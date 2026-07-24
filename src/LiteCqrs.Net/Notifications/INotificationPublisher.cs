namespace LiteCqrs.Notifications;

/// <summary>Pluggable fan-out strategy for <see cref="IPublisher.Publish"/> — swap via
/// <c>LiteCqrs.DependencyInjection.LiteCqrsServiceConfiguration.NotificationPublisherType</c>.
/// Registered as a singleton.</summary>
public interface INotificationPublisher
{
    Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken
    );
}
