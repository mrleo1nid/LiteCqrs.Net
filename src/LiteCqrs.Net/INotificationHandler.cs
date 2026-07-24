namespace LiteCqrs;

/// <summary>Reacts to a published <see cref="INotification"/>. Any number of implementations may
/// be registered for the same closed <c>INotificationHandler&lt;TNotification&gt;</c> — unlike
/// <see cref="ICommandHandler{TCommand, TResponse}"/>/<see cref="IQueryHandler{TQuery, TResponse}"/>,
/// which require exactly one.</summary>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}
