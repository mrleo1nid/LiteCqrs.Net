namespace LiteCqrs;

/// <summary>Handles a single <see cref="IQuery{TResponse}"/> type. Exactly one implementation
/// must be registered per closed <c>IQueryHandler&lt;TQuery,TResponse&gt;</c> — see
/// <c>LiteCqrs.DependencyInjection.ServiceCollectionExtensions.AddLiteCqrs</c>.</summary>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken);
}
