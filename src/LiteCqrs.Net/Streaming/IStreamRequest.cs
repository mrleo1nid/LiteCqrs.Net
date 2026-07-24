namespace LiteCqrs.Streaming;

/// <summary>Marker for a streaming request: a query whose response arrives incrementally as
/// <see cref="IAsyncEnumerable{T}"/> rather than all at once. Dispatched via
/// <see cref="ISender.CreateStream{TResponse}"/>. Kept separate from <see cref="ICommand{TResponse}"/>/
/// <see cref="IQuery{TResponse}"/> because the response shape is fundamentally different
/// (lazily-pulled sequence vs. a single awaited value) and needs its own pipeline
/// (<see cref="IStreamPipelineBehavior{TRequest, TResponse}"/>) — a <see cref="IPipelineBehavior{TRequest, TResponse}"/>
/// composed around <see cref="Task{TResult}"/> can't compose around <see cref="IAsyncEnumerable{T}"/>.</summary>
public interface IStreamRequest<TResponse>;
