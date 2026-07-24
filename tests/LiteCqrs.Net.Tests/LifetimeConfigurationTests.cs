using FluentAssertions;
using LiteCqrs.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Verifies <c>LiteCqrsServiceConfiguration.Lifetime</c> (default <see cref="ServiceLifetime.Scoped"/>,
/// unlike MediatR's Transient default) is honored for scanned handlers.</summary>
public class LifetimeConfigurationTests
{
    private sealed record LifetimeCommand : ICommand<Guid>;

    private sealed class LifetimeCommandHandler : ICommandHandler<LifetimeCommand, Guid>
    {
        public Guid InstanceId { get; } = Guid.NewGuid();

        public Task<Guid> Handle(LifetimeCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(InstanceId);
    }

    [Fact]
    public void DefaultLifetime_IsScoped()
    {
        var services = new ServiceCollection();
        services.AddLiteCqrs(cqrs =>
            cqrs.RegisterServicesFromAssemblyContaining<LifetimeConfigurationTests>()
        );

        var descriptor = services.Single(d => d.ServiceType == typeof(ICommandHandler<LifetimeCommand, Guid>));

        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void ScopedLifetime_SameInstanceWithinScope_DifferentAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddLiteCqrs(cqrs =>
            cqrs.RegisterServicesFromAssemblyContaining<LifetimeConfigurationTests>()
        );
        var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        var first = scope1.ServiceProvider.GetRequiredService<ICommandHandler<LifetimeCommand, Guid>>();
        var second = scope1.ServiceProvider.GetRequiredService<ICommandHandler<LifetimeCommand, Guid>>();
        using var scope2 = provider.CreateScope();
        var third = scope2.ServiceProvider.GetRequiredService<ICommandHandler<LifetimeCommand, Guid>>();

        first.Should().BeSameAs(second);
        first.Should().NotBeSameAs(third);
    }

    [Fact]
    public void TransientLifetime_IsHonored()
    {
        var services = new ServiceCollection();
        services.AddLiteCqrs(cqrs =>
        {
            cqrs.RegisterServicesFromAssemblyContaining<LifetimeConfigurationTests>();
            cqrs.Lifetime = ServiceLifetime.Transient;
        });

        var descriptor = services.Single(d => d.ServiceType == typeof(ICommandHandler<LifetimeCommand, Guid>));

        descriptor.Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void SingletonLifetime_IsHonored()
    {
        var services = new ServiceCollection();
        services.AddLiteCqrs(cqrs =>
        {
            cqrs.RegisterServicesFromAssemblyContaining<LifetimeConfigurationTests>();
            cqrs.Lifetime = ServiceLifetime.Singleton;
        });
        var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        var first = scope1.ServiceProvider.GetRequiredService<ICommandHandler<LifetimeCommand, Guid>>();
        using var scope2 = provider.CreateScope();
        var second = scope2.ServiceProvider.GetRequiredService<ICommandHandler<LifetimeCommand, Guid>>();

        first.Should().BeSameAs(second);
    }
}
