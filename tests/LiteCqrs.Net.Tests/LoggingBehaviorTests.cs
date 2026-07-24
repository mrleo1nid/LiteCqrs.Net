using FluentAssertions;
using LiteCqrs.Behaviors;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Tests for the one ready-made behavior the library ships.</summary>
public class LoggingBehaviorTests
{
    private sealed record TestRequest;

    [Fact]
    public async Task Handle_ReturnsResponseFromNext_Unchanged()
    {
        var behavior = new LoggingBehavior<TestRequest, string>(
            NullLogger<LoggingBehavior<TestRequest, string>>.Instance
        );

        var result = await behavior.Handle(
            new TestRequest(),
            () => Task.FromResult("the-response"),
            CancellationToken.None
        );

        result.Should().Be("the-response");
    }

    [Fact]
    public async Task Handle_CallsNextExactlyOnce()
    {
        var behavior = new LoggingBehavior<TestRequest, string>(
            NullLogger<LoggingBehavior<TestRequest, string>>.Instance
        );
        var callCount = 0;

        await behavior.Handle(
            new TestRequest(),
            () =>
            {
                callCount++;
                return Task.FromResult("x");
            },
            CancellationToken.None
        );

        callCount.Should().Be(1);
    }
}
