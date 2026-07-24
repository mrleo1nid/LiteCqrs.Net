using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using LiteCqrs.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Proves the per-command-type wrapper cache in <see cref="CommandDispatch"/> is
/// process-lifetime (a <c>static</c> dictionary), not tied to a single
/// <see cref="IServiceProvider"/>/scope: dispatching the same command type through two
/// independently-built service providers must reuse the exact same cached wrapper instance rather
/// than rebuilding it via <see cref="Activator.CreateInstance(Type)"/> each time.</summary>
public class DispatchWrapperCachingTests
{
    private sealed record CachedCommand : ICommand<string>;

    private sealed class NoopHandler : ICommandHandler<CachedCommand, string>
    {
        public Task<string> Handle(CachedCommand command, CancellationToken cancellationToken) =>
            Task.FromResult("ok");
    }

    private static ISender BuildSender()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<CachedCommand, string>, NoopHandler>();
        var provider = services.BuildServiceProvider();
        return new Dispatcher(provider);
    }

    private static object GetCachedWrapper()
    {
        var field = typeof(CommandDispatch).GetField(
            "Wrappers",
            BindingFlags.NonPublic | BindingFlags.Static
        )!;
        var dictionary = (ConcurrentDictionary<Type, object>)field.GetValue(null)!;
        return dictionary[typeof(CachedCommand)];
    }

    [Fact]
    public async Task Send_AcrossTwoIndependentProviders_ReusesSameCachedWrapperInstance()
    {
        var senderA = BuildSender();
        await senderA.Send(new CachedCommand());
        var wrapperAfterFirstProvider = GetCachedWrapper();

        var senderB = BuildSender();
        await senderB.Send(new CachedCommand());
        var wrapperAfterSecondProvider = GetCachedWrapper();

        wrapperAfterSecondProvider.Should().BeSameAs(wrapperAfterFirstProvider);
    }
}
