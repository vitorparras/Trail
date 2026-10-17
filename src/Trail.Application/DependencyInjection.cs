using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trail.Application.Abstractions;
using Trail.Application.Telemetry;

namespace Trail.Application;

public static class DependencyInjection
{
    private const string HandlerSuffix = "Handler";

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddHandlersFrom(typeof(DependencyInjection).Assembly);
        services.TryAddSingleton<ApplicationMetrics>();

        return services;
    }

    internal static IServiceCollection AddHandlersFrom(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        var candidates = assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                && !type.IsSubclassOf(typeof(Delegate)));

        foreach (var type in candidates)
        {
            if (type.IsPublic && type.Name.EndsWith(HandlerSuffix, StringComparison.Ordinal))
            {
                services.TryAddScoped(type);
            }

            foreach (var consumerInterface in type.GetInterfaces().Where(IsDomainEventConsumer))
            {
                services.TryAddEnumerable(ServiceDescriptor.Scoped(consumerInterface, type));
            }
        }

        return services;
    }

    private static bool IsDomainEventConsumer(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDomainEventConsumer<>);
}
