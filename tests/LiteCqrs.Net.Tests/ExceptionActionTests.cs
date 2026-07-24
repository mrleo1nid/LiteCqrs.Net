using FluentAssertions;
using LiteCqrs.Exceptions;
using LiteCqrs.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Verifies <see cref="IRequestExceptionAction{TRequest, TException}"/>'s "always runs"
/// guarantee: it fires on every thrown exception exactly once, including when an
/// <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/> subsequently recovers the
/// same exception into a response, and it never itself suppresses the exception from reaching the
/// handler layer.</summary>
public class ExceptionActionTests
{
    private sealed record FailingCommand : ICommand<string>;

    private sealed class ThrowingHandler : ICommandHandler<FailingCommand, string>
    {
        public Task<string> Handle(FailingCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class TrackingAction(List<string> log, string name)
        : IRequestExceptionAction<FailingCommand, InvalidOperationException>
    {
        public Task Execute(
            FailingCommand request,
            InvalidOperationException exception,
            CancellationToken cancellationToken
        )
        {
            log.Add(name);
            return Task.CompletedTask;
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
            state.SetHandled("recovered");
            return Task.CompletedTask;
        }
    }

    private static ISender BuildSender(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<FailingCommand, string>, ThrowingHandler>();
        configure(services);
        var provider = services.BuildServiceProvider();
        return new Dispatcher(provider);
    }

    [Fact]
    public async Task Execute_RunsExactlyOnce_WhenExceptionIsUnrecovered()
    {
        var log = new List<string>();
        var sender = BuildSender(services =>
            services.AddSingleton<IRequestExceptionAction<FailingCommand, InvalidOperationException>>(
                new TrackingAction(log, "action")
            )
        );

        var act = () => sender.Send(new FailingCommand());

        await act.Should().ThrowAsync<InvalidOperationException>();
        log.Should().Equal("action");
    }

    [Fact]
    public async Task Execute_StillRunsExactlyOnce_WhenAHandlerLaterRecoversTheException()
    {
        var log = new List<string>();
        var sender = BuildSender(services =>
        {
            services.AddSingleton<IRequestExceptionAction<FailingCommand, InvalidOperationException>>(
                new TrackingAction(log, "action")
            );
            services.AddScoped<
                IRequestExceptionHandler<FailingCommand, string, InvalidOperationException>,
                RecoveringHandler
            >();
        });

        var result = await sender.Send(new FailingCommand());

        result.Should().Be("recovered");
        log.Should().Equal("action");
    }

    [Fact]
    public async Task Execute_NeverSuppressesTheException_HandlerLayerStillSeesItWhenNoHandlerRecovers()
    {
        var sender = BuildSender(services =>
            services.AddSingleton<IRequestExceptionAction<FailingCommand, InvalidOperationException>>(
                new TrackingAction([], "action")
            )
        );

        var act = () => sender.Send(new FailingCommand());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }
}
