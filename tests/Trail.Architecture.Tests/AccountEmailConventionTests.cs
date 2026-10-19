using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Mono.Cecil;
using Trail.Application.Accounts;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Trail.Architecture.Tests;

public sealed class AccountEmailConventionTests
{
    private const string ConstructorName = ".ctor";
    private const string CloneName = "<Clone>$";
    private const string SetterPrefix = "set_";

    private static readonly string EmailMessageType = typeof(EmailMessage).FullName!;

    private static readonly string[] AllowedBuilders =
    [
        "Trail.Web.Accounts.LocalizedAccountEmailContent",
        "Trail.Application.Accounts.FallbackAccountEmailContent",
        EmailMessageType,
    ];

    private static readonly Lazy<Dictionary<string, ModuleDefinition>> Modules = new(() =>
        new[]
        {
            ArchitectureModel.DomainAssembly,
            ArchitectureModel.ApplicationAssembly,
            ArchitectureModel.InfrastructureAssembly,
            ArchitectureModel.WebAssembly,
            ArchitectureModel.TestsAssembly,
        }.ToDictionary(assembly => assembly.FullName!, assembly => ModuleDefinition.ReadModule(assembly.Location), StringComparer.Ordinal));

    public static readonly IArchRule AccountEmailsShouldBeBuiltOnlyByTheContentPortRule =
        Types().That().FollowCustomPredicate(type => !IsAllowedBuilder(type.FullName), "are not the account e-mail content classes")
            .Should().FollowCustomCondition(
                type => !BuildsEmailMessage(type),
                "not create or copy an EmailMessage, also in lambdas and async methods",
                "creates or copies an EmailMessage outside the account e-mail content classes")
            .WithoutRequiringPositiveResults();

    [Fact]
    public void AccountEmails_AreBuiltOnlyByTheContentPort()
    {
        AccountEmailsShouldBeBuiltOnlyByTheContentPortRule.Check(ArchitectureModel.ProductionArchitecture);
    }

    [Fact]
    public void AccountEmails_AcceptsReadingAnEmailMessage()
    {
        var architecture = ArchitectureModel.BuildArchitectureWithNamespaces("Trail.Application.Violations.AccountEmails.Valid");

        AccountEmailsShouldBeBuiltOnlyByTheContentPortRule.Check(architecture);
    }

    [Theory]
    [InlineData("Trail.Application.Violations.AccountEmails.TargetTyped")]
    [InlineData("Trail.Application.Violations.AccountEmails.FullyQualified")]
    [InlineData("Trail.Application.Violations.AccountEmails.WithExpression")]
    [InlineData("Trail.Application.Violations.AccountEmails.Lambda")]
    [InlineData("Trail.Application.Violations.AccountEmails.AsyncMethod")]
    public void AccountEmails_DetectsAnEmailMessageBuiltOutsideTheContentPort_AsViolation(string violationNamespace)
    {
        var violationArchitecture = ArchitectureModel.BuildArchitectureWithNamespaces(violationNamespace);

        Assert.Throws<FailedArchRuleException>(() =>
            AccountEmailsShouldBeBuiltOnlyByTheContentPortRule.Check(violationArchitecture));
    }

    private static bool IsAllowedBuilder(string fullName) =>
        AllowedBuilders.Any(allowed =>
            fullName == allowed
            || fullName.StartsWith(allowed + "+", StringComparison.Ordinal)
            || fullName.StartsWith(allowed + "/", StringComparison.Ordinal));

    private static bool BuildsEmailMessage(IType type)
    {
        if (!Modules.Value.TryGetValue(type.Assembly.FullName, out var module))
        {
            return false;
        }

        var definition = module.GetType(type.FullName.Replace('+', '/'));

        return definition is not null && WithNestedTypes(definition)
            .SelectMany(nested => nested.Methods)
            .Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions)
            .Any(instruction => instruction.Operand is MethodReference target && CreatesOrCopiesEmailMessage(target));
    }

    private static bool CreatesOrCopiesEmailMessage(MethodReference target) =>
        target.DeclaringType.FullName == EmailMessageType
        && (target.Name == ConstructorName
            || target.Name == CloneName
            || target.Name.StartsWith(SetterPrefix, StringComparison.Ordinal));

    private static IEnumerable<TypeDefinition> WithNestedTypes(TypeDefinition type) =>
        type.NestedTypes.SelectMany(WithNestedTypes).Prepend(type);
}
