namespace LiteCqrs;

/// <summary>
/// Marker for a command: an operation that changes state. Commands have exactly one handler
/// (<see cref="ICommandHandler{TCommand, TResponse}"/>) and are dispatched via
/// <see cref="ISender.Send{TResponse}(ICommand{TResponse}, System.Threading.CancellationToken)"/>.
/// </summary>
/// <typeparam name="TResponse">The type returned by the command's handler.</typeparam>
public interface ICommand<TResponse>;
