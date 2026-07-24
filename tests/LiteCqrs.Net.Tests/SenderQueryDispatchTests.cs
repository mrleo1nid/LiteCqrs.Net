using FluentAssertions;
using LiteCqrs.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Structural twin of <see cref="SenderCommandDispatchTests"/> for
/// <see cref="IQueryHandler{TQuery, TResponse}"/> dispatch.</summary>
public class SenderQueryDispatchTests
{
    private sealed record SumQuery(int A, int B) : IQuery<int>;

    private sealed class SumHandler : IQueryHandler<SumQuery, int>
    {
        public Task<int> Handle(SumQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(query.A + query.B);
    }

    private sealed record LabelQuery : IQuery<string>;

    private sealed class LabelHandler : IQueryHandler<LabelQuery, string>
    {
        public Task<string> Handle(LabelQuery query, CancellationToken cancellationToken) =>
            Task.FromResult("label");
    }

    private static ISender BuildSender()
    {
        var services = new ServiceCollection();
        services.AddScoped<IQueryHandler<SumQuery, int>, SumHandler>();
        services.AddScoped<IQueryHandler<LabelQuery, string>, LabelHandler>();
        var provider = services.BuildServiceProvider();
        return new Dispatcher(provider);
    }

    [Fact]
    public async Task Send_ResolvesRegisteredHandler_AndReturnsItsResult()
    {
        var sender = BuildSender();

        var result = await sender.Send(new SumQuery(2, 3));

        result.Should().Be(5);
    }

    [Fact]
    public async Task Send_WhenNoHandlerRegistered_Throws()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        ISender sender = new Dispatcher(provider);

        var act = () => sender.Send(new SumQuery(1, 1));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Send_TwoDifferentQueryTypes_DispatchIndependently()
    {
        var sender = BuildSender();

        var sum = await sender.Send(new SumQuery(4, 5));
        var label = await sender.Send(new LabelQuery());

        sum.Should().Be(9);
        label.Should().Be("label");
    }
}
