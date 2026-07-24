using Microsoft.Extensions.Logging;

namespace LiteCqrs.Behaviors;

/// <summary>Ready-made, dependency-free pipeline behavior that logs before/after each dispatch.
/// Its only dependency is <see cref="ILogger{TCategoryName}"/> — no coupling to any particular
/// result type or persistence abstraction, unlike most other cross-cutting behaviors a consumer
/// would write for themselves (validation, unit-of-work, authorization). Register it first
/// (outermost) via <c>AddOpenBehavior(typeof(LoggingBehavior&lt;,&gt;))</c> to time/log the whole
/// pipeline, including any behaviors registered after it.</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName}", requestName);

        var response = await next().ConfigureAwait(false);

        logger.LogInformation("Handled {RequestName}", requestName);
        return response;
    }
}
