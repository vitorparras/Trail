using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Trail.Domain.Common;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Trail.Architecture.Tests;

public sealed class HandlerConventionTests
{
    public static readonly IArchRule HandlersShouldFollowConventionsRule =
        Classes().That().ResideInNamespaceMatching(@"^Trail\.Application(\..*)?$")
            .And().HaveNameEndingWith("Handler")
            .Should().BePublic()
            .AndShould().BeSealed()
            .AndShould().FollowCustomCondition(
                VerifyHandlerMethod,
                "have a single public instance method named HandleAsync returning Result or Result<T>",
                "does not satisfy the HandleAsync signature convention")
            .WithoutRequiringPositiveResults();

    [Fact]
    public void Handlers_FollowConventions()
    {
        HandlersShouldFollowConventionsRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void Handlers_AcceptsValidHandler()
    {
        var architecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Application.Violations.Handlers.Valid");

        HandlersShouldFollowConventionsRule.Check(architecture);
    }

    [Fact]
    public void Handlers_AcceptsAllValidResultReturnTypes()
    {
        var architecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Application.Violations.Handlers.ValidVariants");

        HandlersShouldFollowConventionsRule.Check(architecture);
    }

    [Fact]
    public void Handlers_DetectsUnsealedHandler_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Application.Violations.Handlers.Unsealed");

        Assert.Throws<FailedArchRuleException>(() =>
            HandlersShouldFollowConventionsRule.Check(violationArchitecture));
    }

    [Fact]
    public void Handlers_DetectsInternalHandler_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Application.Violations.Handlers.Internal");

        Assert.Throws<FailedArchRuleException>(() =>
            HandlersShouldFollowConventionsRule.Check(violationArchitecture));
    }

    [Fact]
    public void Handlers_DetectsExtraPublicMethod_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Application.Violations.Handlers.ExtraMethod");

        Assert.Throws<FailedArchRuleException>(() =>
            HandlersShouldFollowConventionsRule.Check(violationArchitecture));
    }

    [Fact]
    public void Handlers_DetectsInvalidReturnType_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Application.Violations.Handlers.InvalidReturn");

        Assert.Throws<FailedArchRuleException>(() =>
            HandlersShouldFollowConventionsRule.Check(violationArchitecture));
    }

    [Fact]
    public void Handlers_DetectsWrongMethodName_AsViolation()
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(
            "Trail.Application.Violations.Handlers.WrongMethodName");

        Assert.Throws<FailedArchRuleException>(() =>
            HandlersShouldFollowConventionsRule.Check(violationArchitecture));
    }

    private static bool VerifyHandlerMethod(Class cls)
    {
        var type = Type.GetType(cls.AssemblyQualifiedName)
            ?? ArchitectureModel.ApplicationAssembly.GetType(cls.FullName)
            ?? ArchitectureModel.TestsAssembly.GetType(cls.FullName);

        if (type == null)
        {
            return true;
        }

        var publicMethods = type
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .ToList();

        if (publicMethods.Count != 1)
        {
            return false;
        }

        var handlerMethod = publicMethods[0];
        if (handlerMethod.Name != "HandleAsync")
        {
            return false;
        }

        return IsValidResultReturnType(handlerMethod.ReturnType);
    }

    private static bool IsValidResultReturnType(Type returnType)
    {
        if (returnType == typeof(Result) || returnType == typeof(Task<Result>))
        {
            return true;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            return true;
        }

        if (returnType.IsGenericType
            && returnType.GetGenericTypeDefinition() == typeof(Task<>)
            && returnType.GetGenericArguments()[0].IsGenericType
            && returnType.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(Result<>))
        {
            return true;
        }

        return false;
    }
}
