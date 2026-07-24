using FluentAssertions;
using LiteCqrs.DependencyInjection;
using LiteCqrs.Net.Tests.DuplicateFixture;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Verifies <c>AddLiteCqrs</c>'s assembly scanning: it discovers and registers every
/// command/query handler in a scanned assembly, and eagerly rejects a duplicate handler for the
/// same closed request type. The duplicate scenario deliberately lives in a separate assembly
/// (<see cref="LiteCqrs.Net.Tests.DuplicateFixture"/>) so scanning it never affects any other test
/// that scans the main <c>LiteCqrs.Net.Tests</c> assembly — which must itself stay duplicate-free.</summary>
public class AssemblyScanningRegistrationTests
{
    private sealed record ScannedCommand : ICommand<string>;

    private sealed class ScannedCommandHandler : ICommandHandler<ScannedCommand, string>
    {
        public Task<string> Handle(ScannedCommand command, CancellationToken cancellationToken) =>
            Task.FromResult("scanned-command");
    }

    private sealed record ScannedQuery : IQuery<string>;

    private sealed class ScannedQueryHandler : IQueryHandler<ScannedQuery, string>
    {
        public Task<string> Handle(ScannedQuery query, CancellationToken cancellationToken) =>
            Task.FromResult("scanned-query");
    }

    [Fact]
    public async Task AddLiteCqrs_RegistersEveryDiscoveredHandlerShape()
    {
        var services = new ServiceCollection();
        services.AddLiteCqrs(cqrs =>
            cqrs.RegisterServicesFromAssemblyContaining<AssemblyScanningRegistrationTests>()
        );
        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var commandResult = await sender.Send(new ScannedCommand());
        var queryResult = await sender.Send(new ScannedQuery());

        commandResult.Should().Be("scanned-command");
        queryResult.Should().Be("scanned-query");
    }

    [Fact]
    public void AddLiteCqrs_TwoImplementationsForSameClosedCommand_ThrowsNamingBoth()
    {
        var services = new ServiceCollection();

        var act = () =>
            services.AddLiteCqrs(cqrs =>
                cqrs.RegisterServicesFromAssembly(typeof(FirstDuplicateHandler).Assembly)
            );

        act.Should()
            .Throw<InvalidOperationException>()
            .Which.Message.Should()
            .Contain(nameof(FirstDuplicateHandler))
            .And.Contain(nameof(SecondDuplicateHandler));
    }
}
