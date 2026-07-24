# LiteCqrs.Net

[![Build](https://github.com/mrleo1nid/LiteCqrs.Net/actions/workflows/build.yml/badge.svg)](https://github.com/mrleo1nid/LiteCqrs.Net/actions/workflows/build.yml)
[![Tests](https://github.com/mrleo1nid/LiteCqrs.Net/actions/workflows/test.yml/badge.svg)](https://github.com/mrleo1nid/LiteCqrs.Net/actions/workflows/test.yml)
[![NuGet](https://img.shields.io/nuget/v/LiteCqrs.Net.svg)](https://www.nuget.org/packages/LiteCqrs.Net/)
[![Downloads](https://img.shields.io/nuget/dt/LiteCqrs.Net.svg)](https://www.nuget.org/packages/LiteCqrs.Net/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A lightweight, dynamic-free, CQRS-flavored in-process mediator for .NET — an alternative to
MediatR that keeps an explicit **Command/Query split** instead of a unified `IRequest`.
Multi-targets **netstandard2.0** (max reach: .NET Framework 4.6.1+, Mono, Unity) and **net10.0**
(in-box BCL, no polyfills).

## What it does

- **`ICommand<TResponse>` / `IQuery<TResponse>`** — commands change state, queries read; each has
  exactly one handler (`ICommandHandler<,>` / `IQueryHandler<,>`), dispatched via `ISender.Send(...)`.
- **Dynamic-free dispatch**: per-request-type wrapper classes cached in a process-lifetime
  `ConcurrentDictionary`, built once via reflection and never touched again — no `dynamic`, no
  per-call reflection after the first dispatch of a given request type.
- **Ordered pipeline behaviors** (`IPipelineBehavior<,>`) — logging, validation, transactions,
  authorization, whatever you need. First-registered is outermost. Generic constraints on your
  behavior (e.g. `where TRequest : ICommand<TResponse>`) opt it in only for the requests that
  satisfy them.
- **Notifications / pub-sub** (`INotification` / `INotificationHandler<>` / `IPublisher.Publish`) —
  zero-or-more handlers per notification, run sequentially (safe with scoped, non-thread-safe
  dependencies like an EF Core `DbContext`), failures isolated and aggregated rather than
  fail-fast — pluggable via `INotificationPublisher` if you need a different strategy.
- **Exception behaviors** (`IRequestExceptionHandler<,,>` / `IRequestExceptionAction<,>`) — resolve
  by exception-type hierarchy (most-derived first), let a handler recover an exception into a
  response, or run an action that always fires regardless of recovery.
- **Streaming requests** (`IStreamRequest<>` / `IStreamRequestHandler<,>` / `ISender.CreateStream`)
  — lazy `IAsyncEnumerable<T>` responses with their own composable `IStreamPipelineBehavior<,>`.
- **Flexible registration** — `AddLiteCqrs(cfg => { cfg.RegisterServicesFromAssembly(...); ... })`
  scans one or more assemblies, catches duplicate handler registrations for commands/queries eagerly
  (a footgun MS DI otherwise resolves silently as "last one wins"), and lets you configure the
  service lifetime (defaults to `Scoped`, not MediatR's `Transient`).

## Install

```bash
dotnet add package LiteCqrs.Net
```

## Register

```csharp
using LiteCqrs.Behaviors;
using LiteCqrs.DependencyInjection;

builder.Services.AddLiteCqrs(cqrs =>
{
    cqrs.RegisterServicesFromAssemblyContaining<Program>();
    cqrs.Lifetime = ServiceLifetime.Scoped; // default; set explicitly for clarity if you like

    // First added = outermost.
    cqrs.AddOpenBehavior(typeof(LoggingBehavior<,>));   // ready-made, ships in LiteCqrs.Behaviors
    cqrs.AddOpenBehavior(typeof(MyValidationBehavior<,>));
    cqrs.AddOpenBehavior(typeof(MyUnitOfWorkBehavior<,>));
});
```

Then inject `ISender`:

```csharp
public sealed class CreateOrderEndpoint(ISender sender)
{
    public Task<Result<OrderDto>> Post(CreateOrderCommand command, CancellationToken ct) =>
        sender.Send(command, ct);
}
```

## Why not just MediatR?

MediatR is unified around `IRequest<TResponse>`; LiteCqrs.Net keeps commands and queries as
distinct types on purpose — if your codebase already thinks in CQRS terms, the type system should
say so. Beyond that, the two libraries solve the same problem in a similar shape (pipeline
behaviors, notifications, streaming); pick whichever fits your project's conventions.
