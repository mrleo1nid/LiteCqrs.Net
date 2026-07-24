using System.Runtime.ExceptionServices;
using LiteCqrs.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace LiteCqrs.Internal;

/// <summary>Wraps the terminal command/query handler call with the exception-behavior layers (see
/// <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/> for the full contract):
/// actions run first (innermost, always, never swallow), then handlers get a chance to recover the
/// exception into a response, walking from the most-derived registered exception type up to
/// <see cref="Exception"/> itself.</summary>
internal static class ExceptionPipelineExecutor
{
    public static async Task<TResponse> Execute<TRequest, TResponse, THandler>(
        TRequest request,
        IServiceProvider serviceProvider,
        Func<THandler, TRequest, CancellationToken, Task<TResponse>> invoke,
        CancellationToken cancellationToken
    )
        where THandler : notnull
    {
        var handler = serviceProvider.GetRequiredService<THandler>();

        try
        {
            return await InvokeWithActions(
                    handler,
                    request,
                    serviceProvider,
                    invoke,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var state = new RequestExceptionHandlerState<TResponse>();
            await ResolveHandlers(request, exception, serviceProvider, state, cancellationToken)
                .ConfigureAwait(false);

            if (state.Handled)
                return state.Response!;

            // Rethrow via ExceptionDispatchInfo, not a bare `throw;` here — this catch block is
            // "new" relative to the original throw site, so a bare rethrow would still preserve the
            // stack trace correctly in C#, but ExceptionDispatchInfo also preserves the exact
            // original exception object/state across the await boundary above, which is the
            // guarantee callers of this method should be able to rely on.
            ExceptionDispatchInfo.Capture(exception).Throw();
            throw; // unreachable — Throw() never returns, but the compiler doesn't know that.
        }
    }

    private static async Task<TResponse> InvokeWithActions<TRequest, TResponse, THandler>(
        THandler handler,
        TRequest request,
        IServiceProvider serviceProvider,
        Func<THandler, TRequest, CancellationToken, Task<TResponse>> invoke,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await invoke(handler, request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Actions always run and always rethrow, regardless of whether a handler layer above
            // this one later recovers the same exception — see IRequestExceptionAction's "always"
            // guarantee and why actions must be innermost.
            await RunActions(request, exception, serviceProvider, cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
    }

    private static async Task RunActions<TRequest>(
        TRequest request,
        Exception exception,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        var requestType = typeof(TRequest);
        for (var exceptionType = exception.GetType(); ; exceptionType = exceptionType.BaseType!)
        {
            var actionInterface = typeof(IRequestExceptionAction<,>).MakeGenericType(
                requestType,
                exceptionType
            );
            var method = actionInterface.GetMethod("Execute")!;

            foreach (var action in serviceProvider.GetServices(actionInterface))
            {
                var task = (Task)method.Invoke(action, [request, exception, cancellationToken])!;
                await task.ConfigureAwait(false);
            }

            if (exceptionType == typeof(Exception))
                break;
        }
    }

    private static async Task ResolveHandlers<TRequest, TResponse>(
        TRequest request,
        Exception exception,
        IServiceProvider serviceProvider,
        RequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken
    )
    {
        var requestType = typeof(TRequest);
        for (var exceptionType = exception.GetType(); ; exceptionType = exceptionType.BaseType!)
        {
            var handlerInterface = typeof(IRequestExceptionHandler<,,>).MakeGenericType(
                requestType,
                typeof(TResponse),
                exceptionType
            );
            var method = handlerInterface.GetMethod("Handle")!;

            foreach (var handler in serviceProvider.GetServices(handlerInterface))
            {
                var task = (Task)method.Invoke(handler, [request, exception, state, cancellationToken])!;
                await task.ConfigureAwait(false);
                if (state.Handled)
                    return;
            }

            if (exceptionType == typeof(Exception))
                break;
        }
    }
}
