namespace LiteCqrs.Streaming;

/// <summary>Continuation delegate a stream pipeline behavior calls to obtain the next behavior's
/// (or the terminal handler's) sequence. Unlike <see cref="RequestHandlerDelegate{TResponse}"/>,
/// this returns the sequence synchronously and lazily — no work happens until the caller starts
/// enumerating it.</summary>
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<TResponse>();

/// <summary>
/// Wraps a streaming request dispatch with cross-cutting behavior. Same first-registered-is-outermost
/// composition rule as <see cref="IPipelineBehavior{TRequest, TResponse}"/>
/// (<c>LiteCqrsServiceConfiguration.AddOpenStreamBehavior</c> — call order matters).
///
/// <para>Because <see cref="IAsyncEnumerable{T}"/> is pull-based and lazy, a behavior that wants to
/// intercept, transform, or filter items must itself be an <c>async IAsyncEnumerable&lt;TResponse&gt;</c>
/// method that <c>await foreach</c>s over the <c>next</c> delegate's result and <c>yield return</c>s;
/// a pure passthrough behavior can just <c>return next();</c> without ever awaiting anything.</para>
/// </summary>
public interface IStreamPipelineBehavior<TRequest, TResponse>
{
    IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    );
}
