using FluentAssertions;
using LiteCqrs.Exceptions;
using LiteCqrs.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Verifies exception-handler resolution: exact-type match, fall-back to a base-type
/// handler, derived-type short-circuiting a base-type handler, registration-order among handlers at
/// the same level, and unrecovered exceptions rethrowing with their original type intact.</summary>
public class ExceptionHandlerResolutionTests
{
    private sealed record FailingCommand : ICommand<string>;

    private sealed class ArgumentExceptionThrowingHandler : ICommandHandler<FailingCommand, string>
    {
        public Task<string> Handle(FailingCommand command, CancellationToken cancellationToken) =>
            throw new ArgumentException("bad argument");
    }

    private static ISender BuildSender(Action<IServiceCollection> configureExceptionHandling)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<FailingCommand, string>, ArgumentExceptionThrowingHandler>();
        configureExceptionHandling(services);
        var provider = services.BuildServiceProvider();
        return new Dispatcher(provider);
    }

    private sealed class ExactTypeHandler
        : IRequestExceptionHandler<FailingCommand, string, ArgumentException>
    {
        public Task Handle(
            FailingCommand request,
            ArgumentException exception,
            RequestExceptionHandlerState<string> state,
            CancellationToken cancellationToken
        )
        {
            state.SetHandled("recovered-by-exact-type");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Handle_ExactExceptionTypeHandler_RecoversIntoResponse()
    {
        var sender = BuildSender(services =>
            services.AddScoped<
                IRequestExceptionHandler<FailingCommand, string, ArgumentException>,
                ExactTypeHandler
            >()
        );

        var result = await sender.Send(new FailingCommand());

        result.Should().Be("recovered-by-exact-type");
    }

    private sealed class BaseTypeHandler : IRequestExceptionHandler<FailingCommand, string, Exception>
    {
        public Task Handle(
            FailingCommand request,
            Exception exception,
            RequestExceptionHandlerState<string> state,
            CancellationToken cancellationToken
        )
        {
            state.SetHandled("recovered-by-base-type");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Handle_OnlyBaseTypeHandlerRegistered_StillRecovers()
    {
        var sender = BuildSender(services =>
            services.AddScoped<IRequestExceptionHandler<FailingCommand, string, Exception>, BaseTypeHandler>()
        );

        var result = await sender.Send(new FailingCommand());

        result.Should().Be("recovered-by-base-type");
    }

    [Fact]
    public async Task Handle_BothExactAndBaseTypeHandlersRegistered_ExactTypeWinsAndBaseNeverRuns()
    {
        var baseTypeRan = false;
        var sender = BuildSender(services =>
        {
            services.AddScoped<
                IRequestExceptionHandler<FailingCommand, string, ArgumentException>,
                ExactTypeHandler
            >();
            services.AddSingleton<IRequestExceptionHandler<FailingCommand, string, Exception>>(
                new TrackingBaseHandler(() => baseTypeRan = true)
            );
        });

        var result = await sender.Send(new FailingCommand());

        result.Should().Be("recovered-by-exact-type");
        baseTypeRan.Should().BeFalse();
    }

    private sealed class TrackingBaseHandler(Action onRun)
        : IRequestExceptionHandler<FailingCommand, string, Exception>
    {
        public Task Handle(
            FailingCommand request,
            Exception exception,
            RequestExceptionHandlerState<string> state,
            CancellationToken cancellationToken
        )
        {
            onRun();
            state.SetHandled("should-not-win");
            return Task.CompletedTask;
        }
    }

    private sealed class FirstOfTwoHandler : IRequestExceptionHandler<FailingCommand, string, ArgumentException>
    {
        public Task Handle(
            FailingCommand request,
            ArgumentException exception,
            RequestExceptionHandlerState<string> state,
            CancellationToken cancellationToken
        ) =>
            // Does not call SetHandled — lets the next-registered handler at the same level try.
            Task.CompletedTask;
    }

    private sealed class SecondOfTwoHandler
        : IRequestExceptionHandler<FailingCommand, string, ArgumentException>
    {
        public Task Handle(
            FailingCommand request,
            ArgumentException exception,
            RequestExceptionHandlerState<string> state,
            CancellationToken cancellationToken
        )
        {
            state.SetHandled("second-handler-won");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Handle_MultipleHandlersAtSameLevel_RunInRegistrationOrderUntilOneRecovers()
    {
        var sender = BuildSender(services =>
        {
            services.AddScoped<
                IRequestExceptionHandler<FailingCommand, string, ArgumentException>,
                FirstOfTwoHandler
            >();
            services.AddScoped<
                IRequestExceptionHandler<FailingCommand, string, ArgumentException>,
                SecondOfTwoHandler
            >();
        });

        var result = await sender.Send(new FailingCommand());

        result.Should().Be("second-handler-won");
    }

    [Fact]
    public async Task Handle_NoMatchingHandlerAnywhere_RethrowsOriginalExceptionType()
    {
        var sender = BuildSender(_ => { });

        var act = () => sender.Send(new FailingCommand());

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("bad argument");
    }
}
