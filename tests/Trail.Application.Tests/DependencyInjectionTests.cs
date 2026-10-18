using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Trail.Application.Abstractions;
using Trail.Application.Telemetry;
using Trail.Application.Tests.Samples;
using Trail.Application.Tests.Samples.Telemetry;
using Trail.Web;
using Trail.Web.Dashboard;
using Trail.Web.Planning;

namespace Trail.Application.Tests;

public sealed class DependencyInjectionTests
{
    private static readonly Assembly TestAssembly = typeof(DependencyInjectionTests).Assembly;
    private static readonly string MetricsNamespace = typeof(SampleMetrics).Namespace!;

    [Fact]
    public void AddApplication_OnServiceCollection_ReturnsTheSameCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddApplication();

        Assert.Same(services, returned);
    }

    [Fact]
    public void AddApplication_OnServiceCollection_RegistersApplicationMetricsAsSingleton()
    {
        var services = new ServiceCollection().AddApplication().AddApplication();

        var descriptor = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ApplicationMetrics));
        Assert.Equal(typeof(ApplicationMetrics), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Theory]
    [InlineData(typeof(SampleHandler))]
    [InlineData(typeof(OpenSampleHandler))]
    public void AddHandlersFrom_PublicClassEndingWithHandler_RegistersItAsScopedSelf(Type handler)
    {
        var services = ScanTestAssembly();

        var descriptor = Assert.Single(services, descriptor => descriptor.ServiceType == handler);
        Assert.Equal(handler, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Theory]
    [InlineData(typeof(SampleService))]
    [InlineData(typeof(SampleCallbackHandler))]
    [InlineData(typeof(InternalSampleHandler))]
    [InlineData(typeof(NestedSampleHandler))]
    public void AddHandlersFrom_NotAPublicHandlerClass_IgnoresIt(Type type)
    {
        var services = ScanTestAssembly();

        Assert.DoesNotContain(services, descriptor => descriptor.ImplementationType == type);
    }

    [Fact]
    public void AddHandlersFrom_DomainEventConsumer_RegistersItAsScopedForEachEventInterface()
    {
        var services = ScanTestAssembly();

        var registrations = services
            .Where(descriptor => descriptor.ImplementationType == typeof(SampleEventsConsumer))
            .ToList();

        Assert.Equal(
            [typeof(IDomainEventConsumer<FirstSampleEvent>), typeof(IDomainEventConsumer<SecondSampleEvent>)],
            registrations.Select(descriptor => descriptor.ServiceType).OrderBy(type => type.FullName));
        Assert.All(registrations, descriptor => Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));
    }

    [Fact]
    public void AddHandlersFrom_SeveralConsumersOfTheSameEvent_RegistersEveryConcreteOneWhateverItsVisibility()
    {
        var services = ScanTestAssembly();

        var implementations = services
            .Where(descriptor => descriptor.ServiceType == typeof(IDomainEventConsumer<FirstSampleEvent>))
            .Select(descriptor => descriptor.ImplementationType);

        Assert.Equal(
            [
                typeof(FirstSampleEventMetricsConsumer),
                typeof(InternalSampleEventConsumer),
                typeof(NestedSampleEventConsumer),
                typeof(SampleEventsConsumer),
            ],
            implementations.OrderBy(type => type!.Name));
    }

    [Fact]
    public void AddHandlersFrom_AbstractOrOpenGenericConsumer_IgnoresIt()
    {
        var services = ScanTestAssembly();

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ImplementationType is { IsAbstract: true } or { IsGenericTypeDefinition: true });
    }

    [Fact]
    public void AddHandlersFrom_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var once = ScanTestAssembly();
        var twice = ScanTestAssembly().AddHandlersFrom(typeof(DependencyInjectionTests).Assembly);

        Assert.Equal(once.Count, twice.Count);
    }

    [Fact]
    public void AddHandlersFrom_NullArguments_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddHandlersFrom(typeof(DependencyInjectionTests).Assembly));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddHandlersFrom(null!));
    }

    [Theory]
    [InlineData(typeof(SampleMetrics))]
    [InlineData(typeof(OpenSampleMetrics))]
    public void AddMetricsFrom_PublicConcreteClassEndingWithMetricsInTheNamespace_RegistersItOnceAsSingleton(Type metrics)
    {
        var services = ScanMetrics().AddMetricsFrom(TestAssembly, MetricsNamespace);

        var descriptor = Assert.Single(services, descriptor => descriptor.ServiceType == metrics);
        Assert.Equal(metrics, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Theory]
    [InlineData(typeof(AbstractSampleMetrics))]
    [InlineData(typeof(InternalSampleMetrics))]
    [InlineData(typeof(SampleMetricsRecorder))]
    [InlineData(typeof(OutOfNamespaceSampleMetrics))]
    public void AddMetricsFrom_NotAPublicConcreteMetricsClassOfTheNamespace_IgnoresIt(Type type)
    {
        var services = ScanMetrics();

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == type || descriptor.ImplementationType == type);
    }

    [Fact]
    public void AddMetricsFrom_InvalidArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddMetricsFrom(TestAssembly, MetricsNamespace));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddMetricsFrom(null!, MetricsNamespace));
        Assert.ThrowsAny<ArgumentException>(() => new ServiceCollection().AddMetricsFrom(TestAssembly, " "));
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void AddImplementationsFrom_PublicConcreteImplementations_RegistersEachOnceWithTheGivenLifetime(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection()
            .AddImplementationsFrom<ISampleExtension>(TestAssembly, lifetime)
            .AddImplementationsFrom<ISampleExtension>(TestAssembly, lifetime);

        Assert.Equal(
            [typeof(DerivedSampleExtension), typeof(OpenSampleExtension), typeof(SampleExtension)],
            services.Select(descriptor => descriptor.ImplementationType).OrderBy(type => type!.Name));
        Assert.All(services, descriptor =>
        {
            Assert.Equal(typeof(ISampleExtension), descriptor.ServiceType);
            Assert.Equal(lifetime, descriptor.Lifetime);
        });
    }

    [Theory]
    [InlineData(typeof(SampleExtensionBase))]
    [InlineData(typeof(InternalSampleExtension))]
    [InlineData(typeof(NestedSampleExtension))]
    [InlineData(typeof(GenericSampleExtension<>))]
    public void AddImplementationsFrom_AbstractNonPublicOrOpenGenericImplementation_IgnoresIt(Type type)
    {
        var services = new ServiceCollection().AddImplementationsFrom<ISampleExtension>(TestAssembly, ServiceLifetime.Scoped);

        Assert.DoesNotContain(services, descriptor => descriptor.ImplementationType == type);
    }

    [Fact]
    public void AddImplementationsFrom_NullArguments_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddImplementationsFrom<ISampleExtension>(TestAssembly, ServiceLifetime.Scoped));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddImplementationsFrom<ISampleExtension>(null!, ServiceLifetime.Scoped));
    }

    [Fact]
    public void AddImplementationsFrom_ClassContract_ThrowsArgumentException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddImplementationsFrom<SampleExtensionBase>(TestAssembly, ServiceLifetime.Scoped));
        Assert.Empty(services);
    }

    [Theory]
    [InlineData(typeof(IPlanActivityContributor), typeof(SamplePlanActivityContributor))]
    [InlineData(typeof(IDeletionImpactContributor), typeof(SampleDeletionImpactContributor))]
    public void AddContributorsFrom_ApplicationContributor_RegistersItOnceAsScoped(Type contract, Type contributor)
    {
        var services = new ServiceCollection().AddContributorsFrom(TestAssembly).AddContributorsFrom(TestAssembly);

        var descriptor = Assert.Single(services, descriptor => descriptor.ImplementationType == contributor);
        Assert.Equal(contract, descriptor.ServiceType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddContributorsFrom_TestAssembly_RegistersOnlyTheApplicationContracts()
    {
        var services = new ServiceCollection().AddContributorsFrom(TestAssembly);

        Assert.NotEmpty(services);
        Assert.All(services, descriptor =>
            Assert.Contains(descriptor.ServiceType, new[] { typeof(IPlanActivityContributor), typeof(IDeletionImpactContributor) }));
    }

    [Theory]
    [InlineData(typeof(IDashboardBlock), typeof(SampleDashboardBlock))]
    [InlineData(typeof(IPlanLinkContributor), typeof(SamplePlanLinkContributor))]
    public void AddWebContributorsFrom_WebContributor_RegistersItOnceAsScoped(Type contract, Type contributor)
    {
        var services = new ServiceCollection().AddWebContributorsFrom(TestAssembly).AddWebContributorsFrom(TestAssembly);

        var descriptor = Assert.Single(services, descriptor => descriptor.ImplementationType == contributor);
        Assert.Equal(contract, descriptor.ServiceType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddWebContributorsFrom_TestAssembly_RegistersOnlyTheWebContracts()
    {
        var services = new ServiceCollection().AddWebContributorsFrom(TestAssembly);

        Assert.NotEmpty(services);
        Assert.All(services, descriptor =>
            Assert.Contains(descriptor.ServiceType, new[] { typeof(IDashboardBlock), typeof(IPlanLinkContributor) }));
    }

    private static IServiceCollection ScanTestAssembly() =>
        new ServiceCollection().AddHandlersFrom(typeof(DependencyInjectionTests).Assembly);

    private static IServiceCollection ScanMetrics() =>
        new ServiceCollection().AddMetricsFrom(TestAssembly, MetricsNamespace);

    private sealed class NestedSampleHandler;

    private sealed class NestedSampleExtension : ISampleExtension;

    private sealed class NestedSampleEventConsumer : IDomainEventConsumer<FirstSampleEvent>
    {
        public Task ConsumeAsync(FirstSampleEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
