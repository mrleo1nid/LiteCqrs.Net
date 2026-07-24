using FluentAssertions;
using LiteCqrs.Exceptions;
using LiteCqrs.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Verifies how the exception layer interacts with normal
/// <see cref="IPipelineBehavior{TRequest, TResponse}"/> instances: a behavior positioned outside the
/// exception layer sees a plain successful return when a handler recovers the exception (never an
/// exception), and does see the exception propagate when nothing recovers it.</summary>
public class ExceptionPipelineInteractionTests
{
    private sealed record FailingCommand : ICommand<string>;

    private sealed class ThrowingHandler : ICommandHandler<FailingCommand, string>
    {
        public Task<string> Handle(FailingCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class ObservingBehavior(List<string> log) : IPipelineBehavior<FailingCommand, string>
    {
        public async Task<string> Handle(
            FailingCommand request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken
        )
        {
            try
            {
                var response = await next();
                log.Add($"success:{response}");
                return response;
            }
            catch (Exception exception)
            {
                log.Add($"exception:{exception.GetType().Name}");
                throw;
            }
        }
    }

    private sealed class RecoveringHandler
        : IRequestExceptionHandler<FailingCommand, string, InvalidOperationException>
    {
        public Task Handle(
            FailingCommand request,
            InvalidOperationException exception,
            RequestExceptionHandlerState<string> state,
            CancellationToken cancellationToken
        )
        {
            state.SetHandled("recovered-response");
            return Task.CompletedTask;
        }
    }

    private static ISender BuildSender(List<string> log, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<FailingCommand, string>, ThrowingHandler>();
        services.AddSingleton<IPipelineBehavior<FailingCommand, string>>(new ObservingBehavior(log));
        configure?.Invoke(services);
        var provider = services.BuildServiceProvider();
        return new Dispatcher(provider);
    }

    [Fact]
    public async Task OuterBehavior_SeesPlainSuccess_WhenExceptionIsRecovered()
    {
        var log = new List<string>();
        var sender = BuildSender(
            log,
            services =>
                services.AddScoped<
                    IRequestExceptionHandler<FailingCommand, string, InvalidOperationException>,
                    RecoveringHandler
                >()
        );

        var result = await sender.Send(new FailingCommand());

        result.Should().Be("recovered-response");
        log.Should().Equal("success:recovered-response");
    }

    [Fact]
    public async Task OuterBehavior_SeesTheException_WhenNothingRecoversIt()
    {
        var log = new List<string>();
        var sender = BuildSender(log);

        var act = () => sender.Send(new FailingCommand());

        await act.Should().ThrowAsync<InvalidOperationException>();
        log.Should().Equal("exception:InvalidOperationException");
    }
}
