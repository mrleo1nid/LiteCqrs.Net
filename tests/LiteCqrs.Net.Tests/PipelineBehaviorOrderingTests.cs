using FluentAssertions;
using LiteCqrs.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteCqrs.Net.Tests;

/// <summary>Verifies the pipeline composes in registration order (first-registered = outermost)
/// and that MS DI's open-generic constraint filtering — the mechanism that makes opt-in behaviors
/// like a hypothetical unit-of-work behavior constrained to <c>where TRequest : ICommand&lt;TResponse&gt;</c>
/// work — keeps functioning through the wrapper-based dispatch.</summary>
public class PipelineBehaviorOrderingTests
{
    private sealed record TraceCommand : ICommand<string>;
    private sealed record TraceQuery : IQuery<string>;

    private sealed class TraceCommandHandler : ICommandHandler<TraceCommand, string>
    {
        public Task<string> Handle(TraceCommand command, CancellationToken cancellationToken) =>
            Task.FromResult("handler");
    }

    private sealed class TraceQueryHandler : IQueryHandler<TraceQuery, string>
    {
        public Task<string> Handle(TraceQuery query, CancellationToken cancellationToken) =>
            Task.FromResult("handler");
    }

    private sealed class RecordingBehavior<TRequest, TResponse>(string name, List<string> trace)
        : IPipelineBehavior<TRequest, TResponse>
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken
        )
        {
            trace.Add($"{name}:enter");
            var response = await next();
            trace.Add($"{name}:exit");
            return response;
        }
    }

    /// <summary>Only ever constructible for commands — proves an opt-in constrained behavior is
    /// simply never invoked when dispatching a query, because MS DI can't satisfy the constraint.</summary>
    private sealed class CommandOnlyBehavior<TRequest, TResponse>(List<string> trace)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : ICommand<TResponse>
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken
        )
        {
            trace.Add("command-only:enter");
            var response = await next();
            trace.Add("command-only:exit");
            return response;
        }
    }

    [Fact]
    public async Task Handle_RunsBehaviorsOutermostFirst_ThenHandler()
    {
        var trace = new List<string>();
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<TraceCommand, string>, TraceCommandHandler>();
        services.AddSingleton<IPipelineBehavior<TraceCommand, string>>(
            new RecordingBehavior<TraceCommand, string>("first", trace)
        );
        services.AddSingleton<IPipelineBehavior<TraceCommand, string>>(
            new RecordingBehavior<TraceCommand, string>("second", trace)
        );
        var provider = services.BuildServiceProvider();
        ISender sender = new Dispatcher(provider);

        await sender.Send(new TraceCommand());

        trace.Should().Equal("first:enter", "second:enter", "second:exit", "first:exit");
    }

    [Fact]
    public async Task Handle_CommandConstrainedBehavior_NeverInvokedForQueryDispatch()
    {
        var trace = new List<string>();
        var services = new ServiceCollection();
        services.AddScoped<IQueryHandler<TraceQuery, string>, TraceQueryHandler>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CommandOnlyBehavior<,>));
        services.AddSingleton(trace);
        var provider = services.BuildServiceProvider();
        ISender sender = new Dispatcher(provider);

        await sender.Send(new TraceQuery());

        trace.Should().BeEmpty();
    }
}
