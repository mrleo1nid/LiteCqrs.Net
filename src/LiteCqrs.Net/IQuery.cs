namespace LiteCqrs;

/// <summary>
/// Marker for a query: a side-effect-free read. Queries have exactly one handler
/// (<see cref="IQueryHandler{TQuery, TResponse}"/>) and are dispatched via
/// <see cref="ISender.Send{TResponse}(IQuery{TResponse}, System.Threading.CancellationToken)"/>.
/// </summary>
/// <typeparam name="TResponse">The type returned by the query's handler.</typeparam>
public interface IQuery<TResponse>;
