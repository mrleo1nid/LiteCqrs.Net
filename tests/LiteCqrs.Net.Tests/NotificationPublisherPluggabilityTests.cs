using FluentAssertions;
using LiteCqrs.DependencyInjection;
using LiteCqrs.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Verifies a custom <see cref="INotificationPublisher"/> registered via
/// <c>LiteCqrsServiceConfiguration.NotificationPublisherType</c> is honored instead of the shipped
/// default — proven by a fail-fast strategy stopping a later handler in a chain once an earlier one
/// throws, which <see cref="ForeachContinueOnExceptionPublisher"/> would never do.</summary>
public class NotificationPublisherPluggabilityTests
{
    private sealed record PluggabilityNotification : INotification;

    private sealed class ThrowingFirstHandler : INotificationHandler<PluggabilityNotification>
    {
        public Task Handle(PluggabilityNotification notification, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("first handler failed");
    }

    private sealed class RecordingSecondHandler(List<string> trace) : INotificationHandler<PluggabilityNotification>
    {
        public Task Handle(PluggabilityNotification notification, CancellationToken cancellationToken)
        {
            trace.Add("second-ran");
            return Task.CompletedTask;
        }
    }

    /// <summary>Stops at the first exception instead of running every handler — the opposite of the
    /// shipped default, so its effect is easy to distinguish in a test.</summary>
    private sealed class FailFastPublisher : INotificationPublisher
    {
        public async Task Publish(
            IEnumerable<NotificationHandlerExecutor> handlerExecutors,
            INotification notification,
            CancellationToken cancellationToken
        )
        {
            foreach (var executor in handlerExecutors)
                await executor.HandlerCallback(notification, cancellationToken);
        }
    }

    [Fact]
    public async Task CustomPublisher_IsUsedInsteadOfDefault()
    {
        var trace = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(trace);
        services.AddLiteCqrs(cqrs =>
        {
            cqrs.RegisterServicesFromAssemblyContaining<NotificationPublisherPluggabilityTests>();
            cqrs.NotificationPublisherType = typeof(FailFastPublisher);
        });
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        var act = () => publisher.Publish(new PluggabilityNotification());

        await act.Should().ThrowAsync<InvalidOperationException>();
        // The default publisher would have run RecordingSecondHandler regardless; FailFastPublisher
        // stops at the first throw, so it never runs.
        trace.Should().BeEmpty();
    }

    [Fact]
    public void RegisteredPublisher_IsTheConfiguredType()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new List<string>());
        services.AddLiteCqrs(cqrs =>
        {
            cqrs.RegisterServicesFromAssemblyContaining<NotificationPublisherPluggabilityTests>();
            cqrs.NotificationPublisherType = typeof(FailFastPublisher);
        });
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<INotificationPublisher>().Should().BeOfType<FailFastPublisher>();
    }
}
