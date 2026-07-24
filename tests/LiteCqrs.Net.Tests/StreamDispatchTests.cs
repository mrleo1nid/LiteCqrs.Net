using FluentAssertions;
using LiteCqrs.Internal;
using LiteCqrs.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Dispatch correctness and laziness for <see cref="ISender.CreateStream{TResponse}"/>.</summary>
public class StreamDispatchTests
{
    private sealed record CountStreamRequest(int Count) : IStreamRequest<int>;

    private sealed class CountStreamHandler : IStreamRequestHandler<CountStreamRequest, int>
    {
        public bool Started { get; private set; }

        public async IAsyncEnumerable<int> Handle(
            CountStreamRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            Started = true;
            for (var i = 1; i <= request.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return i;
            }
        }
    }

    private static (ISender Sender, CountStreamHandler Handler) BuildSender()
    {
        var handler = new CountStreamHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IStreamRequestHandler<CountStreamRequest, int>>(handler);
        var provider = services.BuildServiceProvider();
        return (new Dispatcher(provider), handler);
    }

    [Fact]
    public async Task CreateStream_YieldsTheHandlersExactSequence()
    {
        var (sender, _) = BuildSender();
        var items = new List<int>();

        await foreach (var item in sender.CreateStream(new CountStreamRequest(3)))
            items.Add(item);

        items.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void CreateStream_DoesNotStartEnumeration_UntilAwaitForeachBegins()
    {
        var (sender, handler) = BuildSender();

        var stream = sender.CreateStream(new CountStreamRequest(3));

        handler.Started.Should().BeFalse("CreateStream must return lazily — no work before enumeration starts");
        stream.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateStream_CancellationMidEnumeration_StopsFurtherItems()
    {
        var (sender, _) = BuildSender();
        using var cts = new CancellationTokenSource();
        var items = new List<int>();

        var act = async () =>
        {
            await foreach (var item in sender.CreateStream(new CountStreamRequest(5), cts.Token))
            {
                items.Add(item);
                if (item == 2)
                    cts.Cancel();
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        items.Should().Equal(1, 2);
    }

    [Fact]
    public void CreateStream_NoHandlerRegistered_ThrowsImmediately()
    {
        // Unlike the handler's own body (lazy — see DoesNotStartEnumeration above), resolving the
        // handler instance and building the behavior pipeline happens synchronously inside
        // CreateStream itself, so a missing-handler wiring error surfaces immediately rather than
        // being deferred to first enumeration.
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        ISender sender = new Dispatcher(provider);

        var act = () => sender.CreateStream(new CountStreamRequest(1));

        act.Should().Throw<InvalidOperationException>();
    }
}
