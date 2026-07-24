using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace LiteCqrs.Internal;

/// <summary>Structural twin of <see cref="CommandHandlerWrapper{TResponse}"/> for
/// <see cref="IQueryHandler{TQuery, TResponse}"/> — kept separate rather than unified behind one
/// generic "handler kind" abstraction, since <see cref="ICommandHandler{TCommand, TResponse}"/> and
/// <see cref="IQueryHandler{TQuery, TResponse}"/> are deliberately distinct interfaces.</summary>
internal abstract class QueryHandlerWrapper<TResponse>
{
    public abstract Task<TResponse> Handle(
        IQuery<TResponse> query,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    );
}

internal sealed class QueryHandlerWrapperImpl<TQuery, TResponse> : QueryHandlerWrapper<TResponse>
    where TQuery : IQuery<TResponse>
{
    public override Task<TResponse> Handle(
        IQuery<TResponse> query,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        var typedQuery = (TQuery)query;

        Task<TResponse> CallHandler() =>
            ExceptionPipelineExecutor.Execute<TQuery, TResponse, IQueryHandler<TQuery, TResponse>>(
                typedQuery,
                serviceProvider,
                static (handler, request, ct) => handler.Handle(request, ct),
                cancellationToken
            );

        RequestHandlerDelegate<TResponse> pipeline = CallHandler;

        foreach (
            var behavior in serviceProvider
                .GetServices<IPipelineBehavior<TQuery, TResponse>>()
                .Reverse()
        )
        {
            var next = pipeline;
            pipeline = () => behavior.Handle(typedQuery, next, cancellationToken);
        }

        return pipeline();
    }
}

internal static class QueryDispatch
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public static Task<TResponse> Send<TResponse>(
        IQuery<TResponse> query,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        var requestType = query.GetType();
        var responseType = typeof(TResponse);
        var wrapper = (QueryHandlerWrapper<TResponse>)
            Wrappers.GetOrAdd(
                requestType,
                rt =>
                    Activator.CreateInstance(
                        typeof(QueryHandlerWrapperImpl<,>).MakeGenericType(rt, responseType)
                    )!
            );

        return wrapper.Handle(query, serviceProvider, cancellationToken);
    }
}
