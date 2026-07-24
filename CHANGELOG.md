# Changelog

All notable changes to LiteCqrs.Net are documented here. Format loosely follows
[Keep a Changelog](https://keepachangelog.com/); versions follow SemVer.

## [Unreleased]

### Added

- Initial core: `ICommand<TResponse>` / `IQuery<TResponse>` / `ICommandHandler<,>` /
  `IQueryHandler<,>` / `IPipelineBehavior<,>` / `ISender`, dispatched through a dynamic-free,
  cached wrapper mechanism (`LiteCqrs.Internal`) instead of per-call reflection.
- `LiteCqrs.Behaviors.LoggingBehavior<,>` — ready-made, dependency-free pipeline behavior (only
  depends on `ILogger<>`).
- `AddLiteCqrs(...)` DI registration (`LiteCqrs.DependencyInjection`): assembly scanning for
  command/query handlers with eager duplicate-handler detection, ordered `AddOpenBehavior`
  registration, configurable service lifetime (defaults to `Scoped`).
- CI: GitHub Actions workflows for build, tests, and tag-driven NuGet publish (mirrors ThreeXui.Net's
  setup; not yet wired to an actual GitHub repository/NuGet account).
- Notifications/pub-sub: `INotification` / `INotificationHandler<>` / `IPublisher` /
  `ISenderPublisher`, dispatched through the same dynamic-free cached-wrapper mechanism as
  commands/queries. Fan-out strategy is pluggable via
  `LiteCqrsServiceConfiguration.NotificationPublisherType`; the shipped default
  (`ForeachContinueOnExceptionPublisher`) runs handlers sequentially (safe with a shared Scoped,
  non-thread-safe persistence context), isolates each handler's failure, and aggregates every
  failure into a single `AggregateException` once all handlers have run — deliberately not
  MediatR's fail-fast default.
- Exception behaviors: `IRequestExceptionHandler<,,>` / `IRequestExceptionAction<,>`. Resolution
  walks from the thrown exception's most-derived registered type up to `Exception` itself; the
  first handler to call `RequestExceptionHandlerState.SetHandled(...)` stops resolution entirely.
  Actions run innermost (closer to the raw handler call than any exception handler) and always
  fire exactly once per exception — including when a handler later recovers it into a response —
  and never swallow. Both wrap only the terminal command/query handler call, inside every normal
  `IPipelineBehavior<,>`, so an outer behavior (logging, unit-of-work, ...) sees either a plain
  response or a propagating exception, never anything in between.
- Streaming requests: `IStreamRequest<TResponse>` / `IStreamRequestHandler<,>` /
  `IStreamPipelineBehavior<,>` / `ISender.CreateStream`, backed by the same dynamic-free
  cached-wrapper dispatch as commands/queries. Resolving the handler and building the stream
  pipeline happens synchronously inside `CreateStream` (a missing-handler wiring error surfaces
  immediately), but the handler's own body never runs until the caller starts `await foreach`-ing
  the result. No exception-handler equivalent for streams in this release — recovering an
  exception raised mid-enumeration into a response doesn't have a coherent meaning once some items
  have already reached the caller.
