using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Trail.Domain.Common;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Trail.Architecture.Tests;

public sealed class AggregateReferenceTests
{
    public static readonly IArchRule AggregatesShouldOnlyReferenceOtherAggregatesByTypedIdRule =
        Classes().That().FollowCustomPredicate(
                IsAggregateOrEntityInsideAggregate,
                "are aggregates or entities inside aggregates")
            .Should().FollowCustomCondition(
                VerifyAggregateReferences,
                "only reference other aggregates by typed ID",
                "references another aggregate directly")
            .WithoutRequiringPositiveResults();

    [Fact]
    public void Aggregates_ReferenceOtherAggregatesOnlyByTypedId()
    {
        AggregatesShouldOnlyReferenceOtherAggregatesByTypedIdRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void Aggregates_DetectsDirectReferenceToAnotherAggregate_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.Aggregates.Common",
            "Trail.Domain.Violations.Aggregates.DirectProperty");

        Assert.Throws<FailedArchRuleException>(() =>
            AggregatesShouldOnlyReferenceOtherAggregatesByTypedIdRule.Check(violationArchitecture));
    }

    [Fact]
    public void Aggregates_DetectsDirectReferenceInEntityInsideAggregate_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.Aggregates.Common",
            "Trail.Domain.Violations.Aggregates.EntityInsideAggregate");

        Assert.Throws<FailedArchRuleException>(() =>
            AggregatesShouldOnlyReferenceOtherAggregatesByTypedIdRule.Check(violationArchitecture));
    }

    [Fact]
    public void Aggregates_DetectsConstructorParameterToAnotherAggregate_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.Aggregates.Common",
            "Trail.Domain.Violations.Aggregates.ConstructorParam");

        Assert.Throws<FailedArchRuleException>(() =>
            AggregatesShouldOnlyReferenceOtherAggregatesByTypedIdRule.Check(violationArchitecture));
    }

    [Fact]
    public void Aggregates_DetectsMethodParameterToAnotherAggregate_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.Aggregates.Common",
            "Trail.Domain.Violations.Aggregates.MethodParam");

        Assert.Throws<FailedArchRuleException>(() =>
            AggregatesShouldOnlyReferenceOtherAggregatesByTypedIdRule.Check(violationArchitecture));
    }

    [Fact]
    public void Aggregates_DetectsMethodReturnTypeOfAnotherAggregate_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.Aggregates.Common",
            "Trail.Domain.Violations.Aggregates.MethodReturn");

        Assert.Throws<FailedArchRuleException>(() =>
            AggregatesShouldOnlyReferenceOtherAggregatesByTypedIdRule.Check(violationArchitecture));
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

    private static bool VerifyAggregateReferences(Class cls)
    {
        var type = ResolveType(cls.FullName, cls.AssemblyQualifiedName);
        if (type == null)
        {
            return true;
        }

        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (properties.Any(property => IsOrContainsAggregateRoot(property.PropertyType, type)))
        {
            return false;
        }

        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (fields.Any(field => !field.Name.Contains("BackingField", StringComparison.Ordinal)
            && IsOrContainsAggregateRoot(field.FieldType, type)))
        {
            return false;
        }

        var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (constructors.Any(ctor => ctor.GetParameters().Any(parameter => IsOrContainsAggregateRoot(parameter.ParameterType, type))))
        {
            return false;
        }

        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        return !methods.Any(method =>
            !method.IsSpecialName
            && (IsOrContainsAggregateRoot(method.ReturnType, type)
                || method.GetParameters().Any(parameter => IsOrContainsAggregateRoot(parameter.ParameterType, type))));
    }

    private static bool IsOrContainsAggregateRoot(Type type, Type declaringAggregate)
    {
        if (type == declaringAggregate)
        {
            return false;
        }

        if (typeof(IAggregateRoot).IsAssignableFrom(type) && type != typeof(IAggregateRoot))
        {
            return true;
        }

        if (type.IsGenericType)
        {
            return type.GetGenericArguments().Any(argument => IsOrContainsAggregateRoot(argument, declaringAggregate));
        }

        if (type.IsArray)
        {
            return IsOrContainsAggregateRoot(type.GetElementType()!, declaringAggregate);
        }

        return false;
    }

    private static Type? ResolveType(string fullName, string assemblyQualifiedName) =>
        Type.GetType(assemblyQualifiedName)
        ?? ArchitectureModel.DomainAssembly.GetType(fullName)
        ?? ArchitectureModel.TestsAssembly.GetType(fullName);
}
