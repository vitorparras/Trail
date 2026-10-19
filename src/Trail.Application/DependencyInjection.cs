using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trail.Application.Abstractions;
using Trail.Application.Accounts;
using Trail.Application.Fitness;
using Trail.Application.Planning;
using Trail.Application.Practice;
using Trail.Application.StudyTime;
using Trail.Application.Telemetry;

namespace Trail.Application;

public static class DependencyInjection
{
    private const string HandlerSuffix = "Handler";
    private const string MetricsSuffix = "Metrics";

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddHandlersFrom(assembly);
        services.AddMetricsFrom(assembly, typeof(ApplicationMetrics).Namespace!);
        services.AddContributorsFrom(assembly);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IUserCalendar, FallbackUserCalendar>();
        services.TryAddScoped<IAccountIdentityGateway, FallbackAccountIdentityGateway>();
        services.TryAddScoped<IEmailSender, FallbackEmailSender>();
        services.TryAddScoped<IUserSettingsStore, FallbackUserSettingsStore>();
        services.TryAddScoped<IPlanningStore, FallbackPlanningStore>();

        services.AddPracticeApplication();
        services.AddFitnessApplication();
        services.AddStudyTimeApplication();

        return services;
    }

    public static IServiceCollection AddContributorsFrom(this IServiceCollection services, Assembly assembly) =>
        services
            .AddImplementationsFrom<IPlanActivityContributor>(assembly, ServiceLifetime.Scoped)
            .AddImplementationsFrom<IDeletionImpactContributor>(assembly, ServiceLifetime.Scoped);

    public static IServiceCollection AddImplementationsFrom<TService>(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        if (!typeof(TService).IsInterface)
        {
            throw new ArgumentException($"The contract {typeof(TService).FullName} must be an interface.", nameof(TService));
        }

        foreach (var type in PublicConcreteClassesFrom(assembly).Where(type => type.IsAssignableTo(typeof(TService))))
        {
            services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(TService), type, lifetime));
        }

        return services;
    }

    internal static IServiceCollection AddMetricsFrom(this IServiceCollection services, Assembly assembly, string metricsNamespace)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(metricsNamespace);

        var metrics = PublicConcreteClassesFrom(assembly)
            .Where(type => type.Namespace == metricsNamespace && type.Name.EndsWith(MetricsSuffix, StringComparison.Ordinal));

        foreach (var type in metrics)
        {
            services.TryAddSingleton(type);
        }

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

    private static IEnumerable<Type> PublicConcreteClassesFrom(Assembly assembly) =>
        assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsPublic: true, IsAbstract: false, IsGenericTypeDefinition: false }
                && !type.IsSubclassOf(typeof(Delegate)));

    private static bool IsDomainEventConsumer(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDomainEventConsumer<>);
}
