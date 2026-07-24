using LiteCqrs.Notifications;
using LiteCqrs.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace LiteCqrs.Internal;

/// <summary>The single <see cref="ISenderPublisher"/> implementation (registered as <see cref="ISender"/>,
/// <see cref="IPublisher"/>, and <see cref="ISenderPublisher"/> — all three resolve to the same
/// instance per scope, at the configured lifetime, default Scoped — see
/// <c>LiteCqrs.DependencyInjection.LiteCqrsServiceConfiguration</c>). Delegates to the static,
/// dynamic-free <see cref="CommandDispatch"/>/<see cref="QueryDispatch"/>/<see cref="NotificationDispatch"/>
/// caches — this class itself carries no state beyond the injected <see cref="IServiceProvider"/>.</summary>
internal sealed class Dispatcher(IServiceProvider serviceProvider) : ISenderPublisher
{
    public Task<TResponse> Send<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default
    ) => CommandDispatch.Send(command, serviceProvider, cancellationToken);

    public Task<TResponse> Send<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default
    ) => QueryDispatch.Send(query, serviceProvider, cancellationToken);

    public Task Publish(INotification notification, CancellationToken cancellationToken = default)
    {
        var publisher = serviceProvider.GetRequiredService<INotificationPublisher>();
        return NotificationDispatch.Publish(notification, serviceProvider, publisher, cancellationToken);
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default
    ) => StreamDispatch.CreateStream(request, serviceProvider, cancellationToken);
}
