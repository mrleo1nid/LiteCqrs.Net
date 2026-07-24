namespace LiteCqrs.Notifications;

/// <summary>One resolved notification-handler invocation, ready to run — <see cref="HandlerInstance"/>
/// is exposed alongside the callback so a custom <see cref="INotificationPublisher"/> can inspect
/// which handler is about to run (e.g. for logging) without needing to invoke it first.</summary>
public sealed record NotificationHandlerExecutor(
    Func<INotification, CancellationToken, Task> HandlerCallback,
    object HandlerInstance
);
