namespace LiteCqrs.Exceptions;

/// <summary>Mutable out-parameter-style state a matching <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>
/// uses to recover an exception into a response. Once <see cref="SetHandled"/> is called, no
/// further exception handlers run for that dispatch (see <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>
/// for resolution order).</summary>
public sealed class RequestExceptionHandlerState<TResponse>
{
    public bool Handled { get; private set; }
    public TResponse? Response { get; private set; }

    public void SetHandled(TResponse response)
    {
        Response = response;
        Handled = true;
    }
}

/// <summary>
/// Given an exception thrown by the terminal command/query handler, may recover it into a
/// <typeparamref name="TResponse"/> by calling <see cref="RequestExceptionHandlerState{TResponse}.SetHandled"/>.
///
/// <para>Resolution order: handlers are tried from the exception's most-derived registered type up
/// through its base types to <see cref="Exception"/> itself. At each level, matching handlers run in
/// DI registration order; the instant one calls <c>SetHandled</c>, resolution stops entirely — a
/// handler registered for a base exception type never runs once a more-derived handler has already
/// recovered it. If no handler anywhere in the hierarchy recovers the exception, the original
/// exception is rethrown (with its original stack trace preserved).</para>
///
/// <para>This wraps only the terminal handler call, inside every registered
/// <see cref="IPipelineBehavior{TRequest, TResponse}"/> — a normal pipeline behavior sees either a
/// real response or a propagating exception, never anything in between, whether or not that
/// response came from a recovered exception. A behavior like a unit-of-work behavior that commits on
/// a success-shaped response will therefore commit a recovered response too — a handler that wants
/// to recover without permitting a commit should call <c>SetHandled</c> with a failure-shaped
/// response, not a success-shaped stub.</para>
/// </summary>
public interface IRequestExceptionHandler<in TRequest, TResponse, in TException>
    where TException : Exception
{
    Task Handle(
        TRequest request,
        TException exception,
        RequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken
    );
}
