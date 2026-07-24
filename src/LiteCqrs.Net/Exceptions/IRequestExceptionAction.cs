namespace LiteCqrs.Exceptions;

/// <summary>
/// A side-effect-only reaction to an exception thrown by the terminal command/query handler (e.g.
/// logging, metrics) — never recovers it. Runs innermost, closer to the raw handler call than any
/// <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>: this is what makes the
/// "always runs" guarantee true even when an outer exception handler subsequently recovers the same
/// exception into a response — if actions ran outside the handler layer instead, a recovered
/// exception would never reach them, silently breaking that guarantee for exactly the case (a
/// recovered exception) an audit/log side-effect would most want to see. Resolution order across a
/// type hierarchy matches <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>,
/// except every matching action at every level runs (there is no "stop at the first one" — actions
/// have no way to signal "handled").
/// </summary>
public interface IRequestExceptionAction<in TRequest, in TException>
    where TException : Exception
{
    Task Execute(TRequest request, TException exception, CancellationToken cancellationToken);
}
