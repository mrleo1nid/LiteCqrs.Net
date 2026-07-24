using FluentAssertions;
using LiteCqrs.Internal;
using LiteCqrs.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Tests for <see cref="IPublisher.Publish"/> fan-out via the default
/// <see cref="ForeachContinueOnExceptionPublisher"/> strategy: zero handlers is a no-op, N handlers
/// all run, one throwing doesn't stop the rest, and failures aggregate at the end.</summary>
public class NotificationPublishTests
{
    private sealed record PingNotification(string Text) : INotification;

    private sealed class RecordingHandler(List<string> log, string name) : INotificationHandler<PingNotification>
    {
        public Task Handle(PingNotification notification, CancellationToken cancellationToken)
        {
            log.Add($"{name}:{notification.Text}");
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler(string message) : INotificationHandler<PingNotification>
    {
        public Task Handle(PingNotification notification, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(message);
    }

    private static IPublisher BuildPublisher(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<INotificationPublisher, ForeachContinueOnExceptionPublisher>();
        configure?.Invoke(services);
        var provider = services.BuildServiceProvider();
        return new Dispatcher(provider);
    }

    [Fact]
    public async Task Publish_WithNoHandlers_DoesNotThrow()
    {
        var publisher = BuildPublisher();

        var act = () => publisher.Publish(new PingNotification("x"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Publish_WithMultipleHandlers_InvokesAll()
    {
        var log = new List<string>();
        var publisher = BuildPublisher(services =>
        {
            services.AddSingleton<INotificationHandler<PingNotification>>(new RecordingHandler(log, "a"));
            services.AddSingleton<INotificationHandler<PingNotification>>(new RecordingHandler(log, "b"));
        });

        await publisher.Publish(new PingNotification("hi"));

        log.Should().BeEquivalentTo(["a:hi", "b:hi"]);
    }

    [Fact]
    public async Task Publish_OneHandlerThrows_OthersStillRun()
    {
        var log = new List<string>();
        var publisher = BuildPublisher(services =>
        {
            services.AddSingleton<INotificationHandler<PingNotification>>(new RecordingHandler(log, "before"));
            services.AddSingleton<INotificationHandler<PingNotification>>(new ThrowingHandler("boom"));
            services.AddSingleton<INotificationHandler<PingNotification>>(new RecordingHandler(log, "after"));
        });

        var act = () => publisher.Publish(new PingNotification("hi"));

        await act.Should().ThrowAsync<AggregateException>();
        log.Should().BeEquivalentTo(["before:hi", "after:hi"]);
    }

    [Fact]
    public async Task Publish_MultipleHandlersThrow_AggregatesAllExceptions()
    {
        var publisher = BuildPublisher(services =>
        {
            services.AddSingleton<INotificationHandler<PingNotification>>(new ThrowingHandler("first"));
            services.AddSingleton<INotificationHandler<PingNotification>>(new ThrowingHandler("second"));
        });

        var act = () => publisher.Publish(new PingNotification("hi"));

        var exception = await act.Should().ThrowAsync<AggregateException>();
        exception.Which.InnerExceptions.Should().HaveCount(2);
        exception.Which.InnerExceptions.Select(e => e.Message).Should().BeEquivalentTo("first", "second");
    }
}
