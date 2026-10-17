using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Trail.Domain.Common;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Trail.Architecture.Tests;

public sealed class DomainConventionTests
{
    public static readonly IArchRule EntitiesShouldNotHavePublicSettersRule =
        Classes().That().FollowCustomPredicate(
                IsAggregateOrEntityInsideAggregate,
                "are aggregates or entities inside aggregates")
            .Should().FollowCustomCondition(
                VerifyEntityProperties,
                "not have public property setters",
                "has a public property setter")
            .WithoutRequiringPositiveResults();

    public static readonly IArchRule DomainEventsShouldBeSealedRecordsRule =
        Classes().That().ResideInNamespaceMatching(@"^Trail\.Domain(\..*)?$")
            .And().AreAssignableTo(typeof(IDomainEvent))
            .And().AreNotAbstract()
            .Should().BeSealed()
            .AndShould().FollowCustomCondition(
                IsRecordClass,
                "be a record class",
                "is not a record class")
            .WithoutRequiringPositiveResults();

    public static readonly IArchRule TypedIdsShouldFollowConventionsRule =
        Types().That().ResideInNamespaceMatching(@"^Trail\.Domain(\..*)?$")
            .And().ImplementInterface(typeof(ITypedId<>))
            .Should().FollowCustomCondition(
                VerifyTypedIdConvention,
                "follow typed ID conventions (readonly record struct with Value, From, New)",
                "does not follow typed ID conventions")
            .WithoutRequiringPositiveResults();

    [Fact]
    public void Entities_DoNotHavePublicSetters()
    {
        EntitiesShouldNotHavePublicSettersRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void Entities_DetectsPublicSetter_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.Conventions.EntitySetter");

        Assert.Throws<FailedArchRuleException>(() =>
            EntitiesShouldNotHavePublicSettersRule.Check(violationArchitecture));
    }

    [Fact]
    public void Entities_DetectsPublicInit_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.Conventions.EntityInit");

        Assert.Throws<FailedArchRuleException>(() =>
            EntitiesShouldNotHavePublicSettersRule.Check(violationArchitecture));
    }

    [Fact]
    public void Entities_DetectsEntityInsideAggregateWithPublicSetter_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.Conventions.EntityInsideAggregate");

        Assert.Throws<FailedArchRuleException>(() =>
            EntitiesShouldNotHavePublicSettersRule.Check(violationArchitecture));
    }

    [Fact]
    public void DomainEvents_AreSealedRecords()
    {
        DomainEventsShouldBeSealedRecordsRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void DomainEvents_AcceptsValidDomainEvent()
    {
        var architecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.Conventions.ValidEvent");

        DomainEventsShouldBeSealedRecordsRule.Check(architecture);
    }

    [Fact]
    public void DomainEvents_DetectsUnsealedEvent_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.Conventions.UnsealedEvent");

        Assert.Throws<FailedArchRuleException>(() =>
            DomainEventsShouldBeSealedRecordsRule.Check(violationArchitecture));
    }

    [Fact]
    public void DomainEvents_DetectsNonRecordEvent_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.Conventions.NonRecordEvent");

        Assert.Throws<FailedArchRuleException>(() =>
            DomainEventsShouldBeSealedRecordsRule.Check(violationArchitecture));
    }

    [Fact]
    public void TypedIds_FollowConventions()
    {
        TypedIdsShouldFollowConventionsRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void TypedIds_DetectsNonRecordTypedId_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.TypedIds.NonRecord");

        Assert.Throws<FailedArchRuleException>(() =>
            TypedIdsShouldFollowConventionsRule.Check(violationArchitecture));
    }

    [Fact]
    public void TypedIds_DetectsMissingNewMethod_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.TypedIds.MissingNew");

        Assert.Throws<FailedArchRuleException>(() =>
            TypedIdsShouldFollowConventionsRule.Check(violationArchitecture));
    }

    [Fact]
    public void TypedIds_DetectsMissingFromMethod_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.TypedIds.MissingFrom");

        Assert.Throws<FailedArchRuleException>(() =>
            TypedIdsShouldFollowConventionsRule.Check(violationArchitecture));
    }

    [Fact]
    public void TypedIds_DetectsMutableValue_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.TypedIds.Mutable");

        Assert.Throws<FailedArchRuleException>(() =>
            TypedIdsShouldFollowConventionsRule.Check(violationArchitecture));
    }

    [Fact]
    public void TypedIds_DetectsWrongValueType_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildIsolatedArchitecture(
            "Trail.Domain.Violations.TypedIds.WrongValueType");

        var wrongType = violationArchitecture.Types
            .First(t => t.Name == "TypedIdWithWrongValueType");

        Assert.False(VerifyTypedIdConvention(wrongType));
    }

    private static bool IsAggregateOrEntityInsideAggregate(Class cls)
    {
        var type = ResolveType(cls.FullName, cls.AssemblyQualifiedName);
        if (type == null || type.IsAbstract || type.IsInterface)
        {
            return false;
        }

        if (typeof(IAggregateRoot).IsAssignableFrom(type))
        {
            return true;
        }

        var isDomainType = type.Assembly == ArchitectureModel.DomainAssembly
            || type.Namespace?.StartsWith("Trail.Domain", StringComparison.Ordinal) == true;

        if (!isDomainType)
        {
            return false;
        }

        if (type.Namespace == "Trail.Domain.Common")
        {
            return false;
        }

        return !typeof(IDomainEvent).IsAssignableFrom(type);
    }

    private static bool VerifyEntityProperties(Class cls)
    {
        var type = ResolveType(cls.FullName, cls.AssemblyQualifiedName);
        if (type == null)
        {
            return true;
        }

        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        return !properties.Any(property => property.SetMethod is { IsPublic: true });
    }

    private static bool IsRecordClass(Class cls)
    {
        var type = ResolveType(cls.FullName, cls.AssemblyQualifiedName);
        if (type == null)
        {
            return true;
        }

        return type.GetMethod("<Clone>$") != null
            || type.GetProperty("EqualityContract", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) != null;
    }

    private static bool VerifyTypedIdConvention(IType archType)
    {
        var type = ResolveType(archType.FullName, archType.AssemblyQualifiedName);
        if (type == null)
        {
            return true;
        }

        if (!type.IsValueType || type.IsEnum)
        {
            return false;
        }

        var isReadOnly = type.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "IsReadOnlyAttribute");
        if (!isReadOnly)
        {
            return false;
        }

        var isRecord = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(method => method.Name == "PrintMembers");
        if (!isRecord)
        {
            return false;
        }

        var valueProperty = type.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
        if (valueProperty == null || valueProperty.PropertyType != typeof(Guid) || valueProperty.SetMethod is { IsPublic: true })
        {
            return false;
        }

        var fromMethod = type.GetMethod("From", BindingFlags.Static | BindingFlags.Public, [typeof(Guid)]);
        if (fromMethod == null || fromMethod.ReturnType != type)
        {
            return false;
        }

        var newMethod = type.GetMethod("New", BindingFlags.Static | BindingFlags.Public, Type.EmptyTypes);
        return newMethod != null && newMethod.ReturnType == type;
    }

    private static Type? ResolveType(string fullName, string assemblyQualifiedName) =>
        Type.GetType(assemblyQualifiedName)
        ?? ArchitectureModel.DomainAssembly.GetType(fullName)
        ?? ArchitectureModel.TestsAssembly.GetType(fullName);
}
