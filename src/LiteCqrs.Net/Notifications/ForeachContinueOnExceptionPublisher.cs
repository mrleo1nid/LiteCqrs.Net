namespace LiteCqrs.Notifications;

/// <summary>
/// Default <see cref="INotificationPublisher"/>: runs handlers sequentially, isolating each
/// handler's failure from the rest, then surfaces every failure together at the end.
///
/// <para>Sequential rather than parallel (MediatR's own default is fail-fast but still sequential;
/// this deliberately continues past failures too) because handlers are typically registered Scoped
/// and resolve a shared, non-thread-safe persistence context (e.g. an EF Core <c>DbContext</c>
/// wrapper) — fanning out concurrently would risk corrupting or throwing on that shared state, not
/// just be an optimization gone wrong.</para>
///
/// <para>Continue-on-exception rather than fail-fast because independent subscribers to one event
/// shouldn't be able to block each other — a transient failure in one handler (e.g. a flaky outbound
/// notification) has no business preventing an unrelated handler (e.g. an audit-log write) from
/// running.</para>
///
/// <para>Failures are aggregated rather than swallowed: every handler runs, and if any threw, an
/// <see cref="AggregateException"/> is thrown after the loop with all of them in
/// <see cref="AggregateException.InnerExceptions"/> — a caller that cares can catch and inspect it;
/// one that doesn't will still see something fail rather than a silently dropped handler.</para>
/// </summary>
public sealed class ForeachContinueOnExceptionPublisher : INotificationPublisher
{
    public async Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken
    )
    {
        var executors = handlerExecutors as IReadOnlyCollection<NotificationHandlerExecutor>
            ?? handlerExecutors.ToList();

        List<Exception>? exceptions = null;
        foreach (var executor in executors)
        {
            try
            {
                await executor.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                (exceptions ??= []).Add(ex);
            }
        }

        if (exceptions is { Count: > 0 })
        {
            throw new AggregateException(
                $"{exceptions.Count} of {executors.Count} notification handler(s) for "
                    + $"{notification.GetType().Name} threw.",
                exceptions
            );
        }
    }
}
