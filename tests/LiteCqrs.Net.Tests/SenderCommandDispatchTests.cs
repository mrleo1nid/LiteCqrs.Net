using FluentAssertions;
using LiteCqrs.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Dispatch correctness for commands — handler registration is manual here (not via
/// <c>AddLiteCqrs</c>'s assembly scanner) to keep these tests isolated from the rest of the test
/// assembly. <see cref="Dispatcher"/> is accessible via <c>InternalsVisibleTo</c>.</summary>
public class SenderCommandDispatchTests
{
    private sealed record PingCommand(string Text) : ICommand<string>;

    private sealed class PingHandler : ICommandHandler<PingCommand, string>
    {
        public Task<string> Handle(PingCommand command, CancellationToken cancellationToken) =>
            Task.FromResult($"pong:{command.Text}");
    }

    private sealed record OtherCommand : ICommand<int>;

    private sealed class OtherHandler : ICommandHandler<OtherCommand, int>
    {
        public Task<int> Handle(OtherCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(42);
    }

    private static ISender BuildSender(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<PingCommand, string>, PingHandler>();
        services.AddScoped<ICommandHandler<OtherCommand, int>, OtherHandler>();
        configure?.Invoke(services);
        var provider = services.BuildServiceProvider();
        return new Dispatcher(provider);
    }

    [Fact]
    public async Task Send_ResolvesRegisteredHandler_AndReturnsItsResult()
    {
        var sender = BuildSender();

        var result = await sender.Send(new PingCommand("hi"));

        result.Should().Be("pong:hi");
    }

    [Fact]
    public async Task Send_WhenNoHandlerRegistered_Throws()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        ISender sender = new Dispatcher(provider);

        var act = () => sender.Send(new PingCommand("hi"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // A distinct command type from PingCommand — DelegateHandler must not also implement
    // ICommandHandler<PingCommand, string>, or AssemblyScanner's whole-assembly duplicate check
    // (exercised by other test files) would see two handlers for the same closed request type.
    private sealed record TokenProbeCommand : ICommand<string>;

    private sealed class DelegateHandler(Func<CancellationToken, Task<string>> callback)
        : ICommandHandler<TokenProbeCommand, string>
    {
        public Task<string> Handle(TokenProbeCommand command, CancellationToken cancellationToken) =>
            callback(cancellationToken);
    }

    [Fact]
    public async Task Send_PassesCancellationTokenThrough()
    {
        CancellationToken? seen = null;
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<TokenProbeCommand, string>>(_ => new DelegateHandler(ct =>
        {
            seen = ct;
            return Task.FromResult("ok");
        }));
        var provider = services.BuildServiceProvider();
        ISender sender = new Dispatcher(provider);
        using var cts = new CancellationTokenSource();

        await sender.Send(new TokenProbeCommand(), cts.Token);

        seen.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Send_TwoDifferentCommandTypes_DispatchIndependently()
    {
        var sender = BuildSender();

        var pingResult = await sender.Send(new PingCommand("a"));
        var otherResult = await sender.Send(new OtherCommand());

        pingResult.Should().Be("pong:a");
        otherResult.Should().Be(42);
    }
}
