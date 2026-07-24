using FluentAssertions;
using LiteCqrs.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Tests for the <c>AddLiteCqrs</c> DI extension itself: argument/configuration
/// validation and that <see cref="ISender"/> resolves correctly once registered.</summary>
public class ServiceCollectionExtensionsTests
{
    private sealed record ExtCommand : ICommand<string>;

    private sealed class ExtCommandHandler : ICommandHandler<ExtCommand, string>
    {
        public Task<string> Handle(ExtCommand command, CancellationToken cancellationToken) =>
            Task.FromResult("ok");
    }

    // Not an open generic type definition — used to exercise AddOpenBehavior's validation.
    private sealed class ClosedBehavior : IPipelineBehavior<ExtCommand, string>
    {
        public Task<string> Handle(
            ExtCommand request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken
        ) => next();
    }

    // An open generic type that does NOT implement IPipelineBehavior<,>.
    private sealed class NotABehavior<TRequest, TResponse>
    {
    }

    [Fact]
    public void AddLiteCqrs_NullServices_Throws()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddLiteCqrs(cqrs => cqrs.RegisterServicesFromAssemblyContaining<ServiceCollectionExtensionsTests>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddLiteCqrs_NullConfigure_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddLiteCqrs(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddLiteCqrs_NoAssembliesRegistered_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddLiteCqrs(_ => { });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddOpenBehavior_ClosedType_Throws()
    {
        var services = new ServiceCollection();

        var act = () =>
            services.AddLiteCqrs(cqrs =>
            {
                cqrs.RegisterServicesFromAssemblyContaining<ServiceCollectionExtensionsTests>();
                cqrs.AddOpenBehavior(typeof(ClosedBehavior));
            });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddOpenBehavior_NotAPipelineBehavior_Throws()
    {
        var services = new ServiceCollection();

        var act = () =>
            services.AddLiteCqrs(cqrs =>
            {
                cqrs.RegisterServicesFromAssemblyContaining<ServiceCollectionExtensionsTests>();
                cqrs.AddOpenBehavior(typeof(NotABehavior<,>));
            });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegisterServicesFromAssemblyContaining_ResolvesMarkerAssembly()
    {
        var services = new ServiceCollection();
        services.AddLiteCqrs(cqrs =>
            cqrs.RegisterServicesFromAssemblyContaining<ServiceCollectionExtensionsTests>()
        );

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICommandHandler<ExtCommand, string>>()
            .Should()
            .BeOfType<ExtCommandHandler>();
    }

    [Fact]
    public async Task AddLiteCqrs_RegistersWorkingISender()
    {
        var services = new ServiceCollection();
        services.AddLiteCqrs(cqrs =>
            cqrs.RegisterServicesFromAssemblyContaining<ServiceCollectionExtensionsTests>()
        );
        var provider = services.BuildServiceProvider();

        var sender = provider.GetRequiredService<ISender>();
        var result = await sender.Send(new ExtCommand());

        result.Should().Be("ok");
    }
}
