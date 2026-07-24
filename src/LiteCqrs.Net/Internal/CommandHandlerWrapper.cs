using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace LiteCqrs.Internal;

/// <summary>Non-generic-response base so a single <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// can cache one wrapper instance per concrete command type without boxing the response.</summary>
internal abstract class CommandHandlerWrapper<TResponse>
{
    public abstract Task<TResponse> Handle(
        ICommand<TResponse> command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    );
}

internal sealed class CommandHandlerWrapperImpl<TCommand, TResponse> : CommandHandlerWrapper<TResponse>
    where TCommand : ICommand<TResponse>
{
    public override Task<TResponse> Handle(
        ICommand<TResponse> command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        var typedCommand = (TCommand)command;

        Task<TResponse> CallHandler() =>
            ExceptionPipelineExecutor.Execute<TCommand, TResponse, ICommandHandler<TCommand, TResponse>>(
                typedCommand,
                serviceProvider,
                static (handler, request, ct) => handler.Handle(request, ct),
                cancellationToken
            );

        RequestHandlerDelegate<TResponse> pipeline = CallHandler;

        // First-registered-is-outermost: GetServices returns registration order; wrapping from the
        // handler outward (via Reverse()) makes the last-wrapped (= first-registered) run first.
        foreach (
            var behavior in serviceProvider
                .GetServices<IPipelineBehavior<TCommand, TResponse>>()
                .Reverse()
        )
        {
            var next = pipeline;
            pipeline = () => behavior.Handle(typedCommand, next, cancellationToken);
        }

        return pipeline();
    }
}

/// <summary>Per-command-type wrapper cache. <c>static</c> and process-lifetime: command types are
/// compiled into the assembly (no dynamic proliferation), and the <see cref="Dispatcher"/> holding
/// this is registered Scoped — an instance-level cache would empty on every new DI scope (i.e. every
/// request) and pay <see cref="Activator.CreateInstance(Type)"/> again. Wrapper instances carry no
/// captured state (serviceProvider/request are parameters to <see cref="CommandHandlerWrapper{TResponse}.Handle"/>),
/// so sharing one across concurrent scopes/requests is safe.</summary>
internal static class CommandDispatch
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public static Task<TResponse> Send<TResponse>(
        ICommand<TResponse> command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        var requestType = command.GetType();
        var responseType = typeof(TResponse);
        var wrapper = (CommandHandlerWrapper<TResponse>)
            Wrappers.GetOrAdd(
                requestType,
                rt =>
                    Activator.CreateInstance(
                        typeof(CommandHandlerWrapperImpl<,>).MakeGenericType(rt, responseType)
                    )!
            );

        return wrapper.Handle(command, serviceProvider, cancellationToken);
    }
}
