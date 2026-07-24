namespace LiteCqrs;

/// <summary>Handles a single <see cref="ICommand{TResponse}"/> type. Exactly one implementation
/// must be registered per closed <c>ICommandHandler&lt;TCommand,TResponse&gt;</c> — see
/// <c>LiteCqrs.DependencyInjection.ServiceCollectionExtensions.AddLiteCqrs</c>.</summary>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken);
}
