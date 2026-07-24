using System.Collections.Concurrent;
using LiteCqrs.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace LiteCqrs.Internal;

/// <summary>Same dynamic-free, per-notification-type-cached wrapper pattern as
/// <see cref="CommandHandlerWrapper{TResponse}"/>/<see cref="QueryHandlerWrapper{TResponse}"/>, but
/// resolving zero-or-more handlers instead of exactly one, and delegating the actual fan-out
/// strategy to the configured <see cref="INotificationPublisher"/>.</summary>
internal abstract class NotificationHandlerWrapper
{
    public abstract Task Publish(
        INotification notification,
        IServiceProvider serviceProvider,
        INotificationPublisher publisher,
        CancellationToken cancellationToken
    );
}

internal sealed class NotificationHandlerWrapperImpl<TNotification> : NotificationHandlerWrapper
    where TNotification : INotification
{
    public override Task Publish(
        INotification notification,
        IServiceProvider serviceProvider,
        INotificationPublisher publisher,
        CancellationToken cancellationToken
    )
    {
        var typedNotification = (TNotification)notification;
        var executors = serviceProvider
            .GetServices<INotificationHandler<TNotification>>()
            .Select(handler => new NotificationHandlerExecutor(
                (n, ct) => handler.Handle((TNotification)n, ct),
                handler
            ));

        return publisher.Publish(executors, typedNotification, cancellationToken);
    }
}

internal static class NotificationDispatch
{
    private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> Wrappers = new();

    public static Task Publish(
        INotification notification,
        IServiceProvider serviceProvider,
        INotificationPublisher publisher,
        CancellationToken cancellationToken
    )
    {
        var notificationType = notification.GetType();
        var wrapper = Wrappers.GetOrAdd(
            notificationType,
            nt =>
                (NotificationHandlerWrapper)
                    Activator.CreateInstance(
                        typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(nt)
                    )!
        );

        return wrapper.Publish(notification, serviceProvider, publisher, cancellationToken);
    }
}
