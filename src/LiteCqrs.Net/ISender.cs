using LiteCqrs.Streaming;

namespace LiteCqrs;

/// <summary>Dispatches commands, queries, and streaming requests to their single registered
/// handler, running them through the registered pipeline behavior chain.</summary>
public interface ISender
{
    /// <summary>Dispatches <paramref name="command"/> to its <see cref="ICommandHandler{TCommand, TResponse}"/>.</summary>
    Task<TResponse> Send<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default
    );

    /// <summary>Dispatches <paramref name="query"/> to its <see cref="IQueryHandler{TQuery, TResponse}"/>.</summary>
    Task<TResponse> Send<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default
    );

    /// <summary>Dispatches <paramref name="request"/> to its <see cref="IStreamRequestHandler{TRequest, TResponse}"/>.
    /// Resolving the handler and building the stream pipeline happens synchronously inside this
    /// call (so a missing-handler wiring error surfaces immediately), but the handler's own body
    /// does not run at all until the caller starts <c>await foreach</c>-ing the result.</summary>
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default
    );
}
