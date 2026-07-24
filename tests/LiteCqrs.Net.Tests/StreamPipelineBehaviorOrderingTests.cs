using System.Runtime.CompilerServices;
using FluentAssertions;
using LiteCqrs.Internal;
using LiteCqrs.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Verifies <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/> composes with the
/// same first-registered-is-outermost contract as the Task-based <see cref="IPipelineBehavior{TRequest, TResponse}"/>,
/// combining two behaviors (an odd-item filter and a take-first-N cap) to prove both apply, in order.</summary>
public class StreamPipelineBehaviorOrderingTests
{
    private sealed record NumbersRequest(int Count) : IStreamRequest<int>;

    private sealed class NumbersHandler : IStreamRequestHandler<NumbersRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(
            NumbersRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            for (var i = 1; i <= request.Count; i++)
            {
                await Task.Yield();
                yield return i;
            }
        }
    }

    /// <summary>Passthrough behavior that drops odd items.</summary>
    private sealed class EvenOnlyBehavior : IStreamPipelineBehavior<NumbersRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(
            NumbersRequest request,
            StreamHandlerDelegate<int> next,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            await foreach (var item in next().WithCancellation(cancellationToken))
            {
                if (item % 2 == 0)
                    yield return item;
            }
        }
    }

    /// <summary>Caps the sequence to its first <see cref="Limit"/> items.</summary>
    private sealed class TakeBehavior(int limit) : IStreamPipelineBehavior<NumbersRequest, int>
    {
        public int Limit { get; } = limit;

        public async IAsyncEnumerable<int> Handle(
            NumbersRequest request,
            StreamHandlerDelegate<int> next,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            var count = 0;
            await foreach (var item in next().WithCancellation(cancellationToken))
            {
                if (count++ >= Limit)
                    yield break;
                yield return item;
            }
        }
    }

    private static ISender BuildSender(params IStreamPipelineBehavior<NumbersRequest, int>[] behaviorsOutermostFirst)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStreamRequestHandler<NumbersRequest, int>, NumbersHandler>();
        foreach (var behavior in behaviorsOutermostFirst)
            services.AddSingleton(behavior);
        var provider = services.BuildServiceProvider();
        return new Dispatcher(provider);
    }

    [Fact]
    public async Task Handle_EvenOnlyOutermost_TakeInnermost_CapsRawSequenceThenFilters()
    {
        // EvenOnly registered first = outermost (wraps everything inside it, including Take).
        // Take(2), being innermost, wraps the raw handler directly: caps 1..10 to its first 2 items
        // (1, 2). EvenOnly then filters THAT already-capped sequence for even numbers: just 2.
        var sender = BuildSender(new EvenOnlyBehavior(), new TakeBehavior(2));

        var items = new List<int>();
        await foreach (var item in sender.CreateStream(new NumbersRequest(10)))
            items.Add(item);

        items.Should().Equal(2);
    }

    [Fact]
    public async Task Handle_TakeOutermost_EvenOnlyInnermost_FiltersThenCaps()
    {
        // Registered in the opposite order: Take(2) is now outermost, EvenOnly innermost. EvenOnly
        // wraps the raw handler directly: filters 1..10 down to 2,4,6,8,10. Take(2), wrapping
        // EvenOnly's output, then caps THAT filtered sequence to its first 2 items: 2, 4.
        var sender = BuildSender(new TakeBehavior(2), new EvenOnlyBehavior());

        var items = new List<int>();
        await foreach (var item in sender.CreateStream(new NumbersRequest(10)))
            items.Add(item);

        items.Should().Equal(2, 4);
    }
}
