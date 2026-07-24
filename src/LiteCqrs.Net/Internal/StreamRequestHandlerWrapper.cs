using System.Collections.Concurrent;
using LiteCqrs.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace LiteCqrs.Internal;

/// <summary>Same dynamic-free, per-request-type-cached wrapper pattern as
/// <see cref="CommandHandlerWrapper{TResponse}"/>/<see cref="QueryHandlerWrapper{TResponse}"/>, for
/// <see cref="IStreamRequestHandler{TRequest, TResponse}"/>. No exception-handler wrapping here (see
/// <see cref="IStreamRequest{TResponse}"/> for why that's out of scope) — just the stream pipeline.</summary>
internal abstract class StreamRequestHandlerWrapper<TResponse>
{
    public abstract IAsyncEnumerable<TResponse> Handle(
        IStreamRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    );
}

internal sealed class StreamRequestHandlerWrapperImpl<TRequest, TResponse>
    : StreamRequestHandlerWrapper<TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public override IAsyncEnumerable<TResponse> Handle(
        IStreamRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        var typedRequest = (TRequest)request;

        IAsyncEnumerable<TResponse> CallHandler()
        {
            var handler = serviceProvider.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();
            return handler.Handle(typedRequest, cancellationToken);
        }

        StreamHandlerDelegate<TResponse> pipeline = CallHandler;

        foreach (
            var behavior in serviceProvider
                .GetServices<IStreamPipelineBehavior<TRequest, TResponse>>()
                .Reverse()
        )
        {
            var next = pipeline;
            pipeline = () => behavior.Handle(typedRequest, next, cancellationToken);
        }

        // Building the pipeline above is synchronous, cheap DI resolution — the actual work stays
        // lazy: pipeline() just returns the (possibly wrapped) IAsyncEnumerable synchronously,
        // nothing runs until the caller starts `await foreach`-ing it.
        return pipeline();
    }
}

internal static class StreamDispatch
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public static IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        var requestType = request.GetType();
        var responseType = typeof(TResponse);
        var wrapper = (StreamRequestHandlerWrapper<TResponse>)
            Wrappers.GetOrAdd(
                requestType,
                rt =>
                    Activator.CreateInstance(
                        typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(rt, responseType)
                    )!
            );

        return wrapper.Handle(request, serviceProvider, cancellationToken);
    }
}
