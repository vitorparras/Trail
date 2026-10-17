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
        Classes().That().AreAssignableTo(typeof(IAggregateRoot))
            .Should().FollowCustomCondition(
                VerifyAggregateReferences,
                "only reference other aggregates by typed ID",
                "references another aggregate directly");

    [Fact]
    public void Aggregates_ReferenceOtherAggregatesOnlyByTypedId()
    {
        AggregatesShouldOnlyReferenceOtherAggregatesByTypedIdRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void Aggregates_DetectsDirectReferenceToAnotherAggregate_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Domain.Violations.Aggregates");

        Assert.Throws<FailedArchRuleException>(() =>
            AggregatesShouldOnlyReferenceOtherAggregatesByTypedIdRule.Check(violationArchitecture));
    }

    private static bool VerifyAggregateReferences(Class cls)
    {
        var type = Type.GetType(cls.AssemblyQualifiedName)
            ?? ArchitectureModel.DomainAssembly.GetType(cls.FullName)
            ?? ArchitectureModel.TestsAssembly.GetType(cls.FullName);

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
}
