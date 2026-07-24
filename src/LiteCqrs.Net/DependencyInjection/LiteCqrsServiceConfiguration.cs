using System.Reflection;
using LiteCqrs.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace LiteCqrs.DependencyInjection;

/// <summary>Configuration surface for <see cref="ServiceCollectionExtensions.AddLiteCqrs"/>.</summary>
public sealed class LiteCqrsServiceConfiguration
{
    internal List<Assembly> Assemblies { get; } = [];
    internal List<Type> OpenBehaviors { get; } = [];
    internal List<Type> OpenStreamBehaviors { get; } = [];

    /// <summary>Lifetime for scanned handlers and registered open-generic behaviors. Defaults to
    /// <see cref="ServiceLifetime.Scoped"/> — deliberately NOT MediatR's Transient default, since
    /// handlers/behaviors typically resolve a Scoped persistence context (e.g. an EF Core DbContext
    /// wrapper) and must share the same instance within one request/scope.</summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Scoped;

    /// <summary>Overrides the default <c>LiteCqrs.Notifications.INotificationPublisher</c>
    /// (<c>LiteCqrs.Notifications.ForeachContinueOnExceptionPublisher</c>) when set. Must be a
    /// concrete type implementing <c>INotificationPublisher</c>; registered as a singleton.</summary>
    public Type? NotificationPublisherType { get; set; }

    /// <summary>Scans <paramref name="assembly"/> for command/query handlers.</summary>
    public LiteCqrsServiceConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        if (assembly is null)
            throw new ArgumentNullException(nameof(assembly));

        Assemblies.Add(assembly);
        return this;
    }

    /// <summary>Scans each of <paramref name="assemblies"/> for command/query handlers.</summary>
    public LiteCqrsServiceConfiguration RegisterServicesFromAssemblies(params Assembly[] assemblies)
    {
        if (assemblies is null)
            throw new ArgumentNullException(nameof(assemblies));

        foreach (var assembly in assemblies)
            RegisterServicesFromAssembly(assembly);

        return this;
    }

    /// <summary>Convenience for <c>RegisterServicesFromAssembly(typeof(TMarker).Assembly)</c> — pass
    /// any type that lives in the assembly to scan (commonly a <c>DependencyInjection</c> class).</summary>
    public LiteCqrsServiceConfiguration RegisterServicesFromAssemblyContaining<TMarker>() =>
        RegisterServicesFromAssembly(typeof(TMarker).Assembly);

    /// <summary>Registers an open-generic <see cref="IPipelineBehavior{TRequest, TResponse}"/>
    /// (e.g. <c>typeof(MyBehavior&lt;,&gt;)</c>). Call order is significant: the first behavior
    /// added is the outermost in the pipeline — it runs first, and its call into the delegate it's
    /// handed reaches the next-added behavior, down to the terminal handler.</summary>
    public LiteCqrsServiceConfiguration AddOpenBehavior(Type openBehaviorType)
    {
        ValidateOpenGeneric(openBehaviorType, typeof(IPipelineBehavior<,>));
        OpenBehaviors.Add(openBehaviorType);
        return this;
    }

    /// <summary>Same ordering contract as <see cref="AddOpenBehavior"/>, for
    /// <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/>.</summary>
    public LiteCqrsServiceConfiguration AddOpenStreamBehavior(Type openStreamBehaviorType)
    {
        ValidateOpenGeneric(openStreamBehaviorType, typeof(IStreamPipelineBehavior<,>));
        OpenStreamBehaviors.Add(openStreamBehaviorType);
        return this;
    }

    private static void ValidateOpenGeneric(Type type, Type openInterface)
    {
        if (type is null)
            throw new ArgumentNullException(nameof(type));
        if (!type.IsGenericTypeDefinition)
            throw new InvalidOperationException(
                $"{type.FullName} must be an open generic type definition (e.g. typeof(MyBehavior<,>))."
            );

        var implementsOpenInterface = type.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterface);
        if (!implementsOpenInterface)
            throw new InvalidOperationException(
                $"{type.FullName} must implement {openInterface.Name}."
            );
    }
}
