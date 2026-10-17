using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Conditions;
using ArchUnitNET.xUnitV3;
using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Violations.Events.Consumed;
using Trail.Domain.Violations.Events.Orphan;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Trail.Architecture.Tests;

public sealed class DomainEventConsumerTests
{
    public static readonly IArchRule DomainEventsShouldHaveConsumersInApplicationRule =
        Classes().That().ResideInNamespaceMatching(@"^Trail\.Domain(\..*)?$")
            .And().AreAssignableTo(typeof(IDomainEvent))
            .And().AreNotAbstract()
            .Should().FollowCustomCondition(
                new ArchitectureCondition<Class>(
                    (classes, architecture) =>
                    {
                        var consumed = GetConsumedEventTypes(architecture);

                        return classes.Select(cls =>
                        {
                            var type = Type.GetType(cls.AssemblyQualifiedName)
                                ?? ArchitectureModel.DomainAssembly.GetType(cls.FullName)
                                ?? ArchitectureModel.TestsAssembly.GetType(cls.FullName);

                            var isConsumed = type != null && consumed.Contains(type);
                            return new ConditionResult(cls, isConsumed, $"{cls.FullName} does not have a consumer in Application");
                        });
                    },
                    "have at least one consumer in Application"))
            .WithoutRequiringPositiveResults();

    [Fact]
    public void DomainEvents_EveryConcreteEventHasConsumerInApplication()
    {
        DomainEventsShouldHaveConsumersInApplicationRule.Check(ArchitectureModel.ProductionArchitecture);

        var unconsumed = FindUnconsumedDomainEvents(ArchitectureModel.ProductionArchitecture);
        Assert.Empty(unconsumed);
    }

    [Fact]
    public void DomainEvents_AcceptsEventWithConsumer()
    {
        var architecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.Events.Consumed",
            "Trail.Application.Violations.Consumers");

        DomainEventsShouldHaveConsumersInApplicationRule.Check(architecture);

        var unconsumed = FindUnconsumedDomainEvents(architecture);
        Assert.DoesNotContain(typeof(ConsumedSampleEvent), unconsumed);
    }

    [Fact]
    public void DomainEvents_DetectsOrphanEventWithoutConsumer_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.Events.Orphan");

        Assert.Throws<FailedArchRuleException>(() =>
            DomainEventsShouldHaveConsumersInApplicationRule.Check(violationArchitecture));

        var unconsumed = FindUnconsumedDomainEvents(violationArchitecture);
        Assert.Contains(typeof(OrphanSampleEvent), unconsumed);
    }

    public static IReadOnlySet<Type> GetConsumedEventTypes(ArchUnitNET.Domain.Architecture architecture)
    {
        return architecture.Types
            .Where(type => type.Namespace.FullName.StartsWith("Trail.Application", StringComparison.Ordinal))
            .Select(type => Type.GetType(type.AssemblyQualifiedName)
                ?? ArchitectureModel.ApplicationAssembly.GetType(type.FullName)
                ?? ArchitectureModel.TestsAssembly.GetType(type.FullName))
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type!.GetInterfaces())
            .Where(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDomainEventConsumer<>))
            .Select(iface => iface.GetGenericArguments()[0])
            .ToHashSet();
    }

    public static IReadOnlyList<Type> FindUnconsumedDomainEvents(ArchUnitNET.Domain.Architecture architecture)
    {
        var events = architecture.Types
            .Where(type => type.Namespace.FullName.StartsWith("Trail.Domain", StringComparison.Ordinal))
            .Select(type => Type.GetType(type.AssemblyQualifiedName)
                ?? ArchitectureModel.DomainAssembly.GetType(type.FullName)
                ?? ArchitectureModel.TestsAssembly.GetType(type.FullName))
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IDomainEvent).IsAssignableFrom(type))
            .Select(type => type!)
            .ToList();

        var consumed = GetConsumedEventTypes(architecture);

        return events.Where(domainEvent => !consumed.Contains(domainEvent)).ToList();
    }
}
