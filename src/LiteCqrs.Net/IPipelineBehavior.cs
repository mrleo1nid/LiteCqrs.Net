namespace LiteCqrs;

/// <summary>Continuation delegate a pipeline behavior calls to invoke the next behavior (or the
/// terminal handler, if this is the innermost behavior).</summary>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Wraps a command/query dispatch with cross-cutting behavior (logging, validation, transactions,
/// authorization, ...). Multiple behaviors compose into a chain: the first one registered via
/// <c>LiteCqrs.DependencyInjection.LiteCqrsServiceConfiguration.AddOpenBehavior</c> is the
/// outermost — it runs first, and calling the <c>next</c> delegate it's handed invokes the
/// next-registered behavior, down to the terminal handler.
/// </summary>
/// <remarks>
/// A behavior constrained to <c>where TRequest : ICommand&lt;TResponse&gt;</c> (or <c>IQuery&lt;&gt;</c>,
/// or an app-specific marker interface) is only ever constructed for requests satisfying that
/// constraint — Microsoft.Extensions.DependencyInjection silently skips open-generic services whose
/// constraints the closed request type doesn't satisfy. This is how opt-in behaviors work.
/// </remarks>
public interface IPipelineBehavior<TRequest, TResponse>
{
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    );
}
