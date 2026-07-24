using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace LiteCqrs.DependencyInjection;

/// <summary>Generalized replacement for the hand-rolled reflection loop PnvPanel used to write for
/// every consuming application: finds closed generic implementations of a set of open handler
/// interfaces across the configured assemblies and registers them.</summary>
internal static class AssemblyScanner
{
    public static void RegisterHandlers(
        IServiceCollection services,
        IReadOnlyCollection<Assembly> assemblies,
        ServiceLifetime lifetime,
        IReadOnlyCollection<Type> openHandlerInterfaces,
        IReadOnlyCollection<Type> allowMultipleImplementations
    )
    {
        var discovered = new List<(Type Service, Type Implementation)>();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type is not { IsClass: true, IsAbstract: false })
                    continue;

                foreach (var iface in type.GetInterfaces())
                {
                    if (!iface.IsGenericType)
                        continue;

                    var openIface = iface.GetGenericTypeDefinition();
                    if (!openHandlerInterfaces.Contains(openIface))
                        continue;

                    discovered.Add((iface, type));
                }
            }
        }

        // A closed handler service (e.g. ICommandHandler<CreateFoo, Result<Guid>>) must resolve to
        // exactly one implementation — Microsoft.Extensions.DependencyInjection silently resolves
        // "last registered wins" for a duplicate non-enumerable service, which is a real footgun for
        // CQRS handler registration. INotificationHandler<> (and any other interface passed in
        // allowMultipleImplementations) is exempt: many handlers per notification is the point.
        foreach (var group in discovered.GroupBy(d => d.Service))
        {
            var openService = group.Key.GetGenericTypeDefinition();
            if (allowMultipleImplementations.Contains(openService))
                continue;

            var implementations = group.Select(g => g.Implementation).Distinct().ToList();
            if (implementations.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple implementations found for {group.Key}: "
                        + string.Join(", ", implementations.Select(i => i.FullName))
                        + ". Exactly one handler must be registered per closed request type."
                );
            }
        }

        foreach (var (service, implementation) in discovered)
            services.Add(new ServiceDescriptor(service, implementation, lifetime));
    }
}
