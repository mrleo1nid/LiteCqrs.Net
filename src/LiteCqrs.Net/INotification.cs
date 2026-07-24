namespace LiteCqrs;

/// <summary>Marker for a notification: an event zero or more handlers may react to, published via
/// <see cref="IPublisher.Publish(INotification, System.Threading.CancellationToken)"/>. Unlike
/// <see cref="ICommand{TResponse}"/>/<see cref="IQuery{TResponse}"/> (exactly one handler required),
/// a notification may have any number of handlers, including zero.</summary>
public interface INotification;
