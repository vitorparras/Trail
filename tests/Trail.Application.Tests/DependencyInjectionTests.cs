using Microsoft.Extensions.DependencyInjection;
using Trail.Application.Abstractions;
using Trail.Application.Telemetry;
using Trail.Application.Tests.Samples;

namespace Trail.Application.Tests;

public sealed class DependencyInjectionTests
{
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

    private static IServiceCollection ScanTestAssembly() =>
        new ServiceCollection().AddHandlersFrom(typeof(DependencyInjectionTests).Assembly);

    private sealed class NestedSampleHandler;

    private sealed class NestedSampleEventConsumer : IDomainEventConsumer<FirstSampleEvent>
    {
        public Task ConsumeAsync(FirstSampleEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
