using LiteCqrs.Exceptions;
using LiteCqrs.Internal;
using LiteCqrs.Notifications;
using LiteCqrs.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace LiteCqrs.DependencyInjection;

/// <summary>DI wiring for LiteCqrs.Net: registers the dispatcher (<see cref="ISender"/>/
/// <see cref="IPublisher"/>/<see cref="ISenderPublisher"/>), scans the configured assemblies for
/// command/query/notification/exception/stream handlers, and registers pipeline behaviors in the
/// caller-specified order.</summary>
public static class ServiceCollectionExtensions
{
    private static readonly Type[] HandlerInterfaces =
    [
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
        typeof(INotificationHandler<>),
        typeof(IRequestExceptionHandler<,,>),
        typeof(IRequestExceptionAction<,>),
        typeof(IStreamRequestHandler<,>),
    ];

    // Many-per-closed-type is valid for all of these: multiple notification handlers all run;
    // multiple exception handlers/actions for the same (request, exception) pair run in
    // registration order (handlers stop at the first that recovers; actions all run regardless).
    // Only ICommandHandler<,>/IQueryHandler<,> require exactly one implementation.
    private static readonly Type[] AllowMultipleImplementations =
    [
        typeof(INotificationHandler<>),
        typeof(IRequestExceptionHandler<,,>),
        typeof(IRequestExceptionAction<,>),
    ];

    public static IServiceCollection AddLiteCqrs(
        this IServiceCollection services,
        Action<LiteCqrsServiceConfiguration> configure
    )
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        var config = new LiteCqrsServiceConfiguration();
        configure(config);

        if (config.Assemblies.Count == 0)
            throw new InvalidOperationException(
                $"{nameof(LiteCqrsServiceConfiguration)}: call "
                    + $"{nameof(LiteCqrsServiceConfiguration.RegisterServicesFromAssembly)} (or "
                    + "...Assemblies/...Containing<T>) at least once before AddLiteCqrs completes."
            );

        AssemblyScanner.RegisterHandlers(
            services,
            config.Assemblies,
            config.Lifetime,
            HandlerInterfaces,
            AllowMultipleImplementations
        );

        foreach (var openBehavior in config.OpenBehaviors)
            services.Add(new ServiceDescriptor(typeof(IPipelineBehavior<,>), openBehavior, config.Lifetime));

        foreach (var openStreamBehavior in config.OpenStreamBehaviors)
            services.Add(
                new ServiceDescriptor(typeof(IStreamPipelineBehavior<,>), openStreamBehavior, config.Lifetime)
            );

        services.Add(new ServiceDescriptor(typeof(Dispatcher), typeof(Dispatcher), config.Lifetime));
        services.Add(
            new ServiceDescriptor(
                typeof(ISender),
                static sp => sp.GetRequiredService<Dispatcher>(),
                config.Lifetime
            )
        );
        services.Add(
            new ServiceDescriptor(
                typeof(IPublisher),
                static sp => sp.GetRequiredService<Dispatcher>(),
                config.Lifetime
            )
        );
        services.Add(
            new ServiceDescriptor(
                typeof(ISenderPublisher),
                static sp => sp.GetRequiredService<Dispatcher>(),
                config.Lifetime
            )
        );

        RegisterNotificationPublisher(services, config.NotificationPublisherType);

        return services;
    }

    private static void RegisterNotificationPublisher(IServiceCollection services, Type? publisherType)
    {
        if (publisherType is null)
        {
            services.AddSingleton<INotificationPublisher, ForeachContinueOnExceptionPublisher>();
            return;
        }

        if (publisherType is not { IsClass: true, IsAbstract: false })
            throw new InvalidOperationException(
                $"{nameof(LiteCqrsServiceConfiguration.NotificationPublisherType)} ({publisherType.FullName}) "
                    + "must be a concrete class."
            );
        if (!typeof(INotificationPublisher).IsAssignableFrom(publisherType))
            throw new InvalidOperationException(
                $"{nameof(LiteCqrsServiceConfiguration.NotificationPublisherType)} ({publisherType.FullName}) "
                    + $"must implement {nameof(INotificationPublisher)}."
            );

        services.AddSingleton(typeof(INotificationPublisher), publisherType);
    }
}
