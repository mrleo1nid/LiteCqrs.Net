namespace LiteCqrs.Streaming;

/// <summary>Handles a single <see cref="IStreamRequest{TResponse}"/> type. Exactly one
/// implementation must be registered per closed <c>IStreamRequestHandler&lt;TRequest,TResponse&gt;</c>,
/// same as <see cref="ICommandHandler{TCommand, TResponse}"/>/<see cref="IQueryHandler{TQuery, TResponse}"/>.</summary>
public interface IStreamRequestHandler<in TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
