namespace LiteCqrs;

/// <summary>Publishes a notification to every registered <see cref="INotificationHandler{TNotification}"/>.
/// Kept separate from <see cref="ISender"/> on purpose: <c>Send</c> has "exactly one handler must
/// exist" semantics, <c>Publish</c> has "zero or more, isolated failures" — merging them into one
/// interface invites reaching for the wrong one. See <see cref="ISenderPublisher"/> for a
/// convenience composition of both.</summary>
public interface IPublisher
{
    /// <summary>Runs every registered handler for <paramref name="notification"/>'s concrete type,
    /// via the configured <c>LiteCqrs.Notifications.INotificationPublisher</c> strategy (defaults to
    /// sequential execution with isolated, aggregated failures — see
    /// <c>LiteCqrs.Notifications.ForeachContinueOnExceptionPublisher</c>).</summary>
    Task Publish(INotification notification, CancellationToken cancellationToken = default);
}
