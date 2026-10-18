using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Localization;
using Trail.Web.Results;

namespace Trail.Web.Tests.Results;

public sealed class MessageCodeTranslationTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    private static readonly string[] ScannedProjects = ["Trail.Domain", "Trail.Application", "Trail.Infrastructure", "Trail.Web"];
    private static readonly string[] MessageFactoryMethods = ["Error", "Warning", "Info", "Success"];
    private static readonly string[] BuildOutputDirectories = ["bin", "obj"];

    private static readonly HashSet<(string Type, string Expression)> AllowedComputedCodes =
    [
        ("Notification", "code"),
        ("AccountIdentityGateway", "$\"accounts.identity.{e.Code.ToLowerInvariant()}\""),
    ];

    private static readonly Lazy<MessageCodeScan> Scan = new(ScanSources);

    [Fact]
    public void MessageCodeArguments_InProductionCode_AreLiteralsConstantsOrAllowListed()
    {
        var computed = Scan.Value.Unresolved
            .Where(argument => !AllowedComputedCodes.Contains((argument.Type, argument.Expression)))
            .ToList();

        Assert.Empty(computed);
    }

    [Fact]
    public void AllowListedComputedCodes_AreStillInUse()
    {
        var used = Scan.Value.Unresolved.Select(argument => (argument.Type, argument.Expression)).ToHashSet();
        var stale = AllowedComputedCodes.Where(entry => !used.Contains(entry)).ToList();

        Assert.Empty(stale);
    }

    [Fact]
    public void MessageCodes_InProductionCode_HaveATranslationInTheResourceOfTheirPrefix()
    {
        var localizerFactory = factory.Services.GetRequiredService<IStringLocalizerFactory>();
        var codes = Scan.Value.Codes;

        var untranslated = codes
            .Where(code => localizerFactory.CreateForCode(code)[code].ResourceNotFound)
            .Select(code => $"{code} ({MessageResourceRegistry.ResourceTypeFor(code).Name}.resx)");

        Assert.NotEmpty(codes);
        Assert.Empty(untranslated);
    }

    private static MessageCodeScan ScanSources()
    {
        var sourceRoot = Path.Combine(RepositoryRoot().FullName, "src");
        var roots = ScannedProjects
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(sourceRoot, project), "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(sourceRoot, path))
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: Path.GetRelativePath(sourceRoot, path)).GetRoot())
            .ToList();
        var constants = StringConstants(roots);

        var codes = new SortedSet<string>(StringComparer.Ordinal);
        var unresolved = new List<UnresolvedCode>();

        foreach (var argument in roots.SelectMany(root => root.DescendantNodes().OfType<InvocationExpressionSyntax>()).Select(CodeArgument).OfType<ArgumentSyntax>())
        {
            var containingType = ContainingTypeName(argument);
            var resolved = Resolve(argument.Expression, containingType, constants);
            if (resolved is null)
            {
                unresolved.Add(new UnresolvedCode(containingType, argument.Expression.ToString(), Location(argument)));
            }
            else
            {
                codes.UnionWith(resolved);
            }
        }

        return new MessageCodeScan(codes, unresolved);
    }

    private static List<string>? Resolve(ExpressionSyntax expression, string containingType, Dictionary<string, List<string>> constants) =>
        expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => [literal.Token.ValueText],
            ParenthesizedExpressionSyntax parenthesized => Resolve(parenthesized.Expression, containingType, constants),
            ConditionalExpressionSyntax conditional =>
                Resolve(conditional.WhenTrue, containingType, constants) is { } whenTrue
                && Resolve(conditional.WhenFalse, containingType, constants) is { } whenFalse
                    ? [.. whenTrue, .. whenFalse]
                    : null,
            IdentifierNameSyntax identifier => Constant($"{containingType}.{identifier.Identifier.ValueText}", constants),
            MemberAccessExpressionSyntax { Expression: var owner, Name: IdentifierNameSyntax member } =>
                Constant($"{SimpleName(owner)}.{member.Identifier.ValueText}", constants),
            _ => null,
        };

    private static List<string>? Constant(string qualifiedName, Dictionary<string, List<string>> constants) =>
        constants.TryGetValue(qualifiedName, out var values) && values.Count == 1 ? values : null;

    private static Dictionary<string, List<string>> StringConstants(IEnumerable<SyntaxNode> roots)
    {
        var constants = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var field in roots.SelectMany(root => root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            .Where(field => field.Modifiers.Any(SyntaxKind.ConstKeyword)))
        {
            var typeName = ContainingTypeName(field);
            foreach (var variable in field.Declaration.Variables)
            {
                if (variable.Initializer?.Value is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    var key = $"{typeName}.{variable.Identifier.ValueText}";
                    if (!constants.TryGetValue(key, out var values))
                    {
                        constants[key] = values = [];
                    }

                    values.Add(literal.Token.ValueText);
                }
            }
        }

        return constants;
    }

    private static ArgumentSyntax? CodeArgument(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: var method } access
                when IsMessageType(access.Expression) && MessageFactoryMethods.Contains(method) => Argument(invocation, position: 0),
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "AddErrorIf" } => Argument(invocation, position: 1),
            IdentifierNameSyntax { Identifier.ValueText: "AddErrorIf" } => Argument(invocation, position: 1),
            _ => null,
        };

    private static bool IsMessageType(ExpressionSyntax expression) => SimpleName(expression) == "Message";

    private static string? SimpleName(ExpressionSyntax expression) =>
        expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => null,
        };

    private static ArgumentSyntax? Argument(InvocationExpressionSyntax invocation, int position)
    {
        var arguments = invocation.ArgumentList.Arguments;

        return arguments.FirstOrDefault(argument => argument.NameColon?.Name.Identifier.ValueText == "code")
            ?? (position < arguments.Count && arguments[position].NameColon is null ? arguments[position] : null);
    }

    private static string ContainingTypeName(SyntaxNode node) =>
        node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? string.Empty;

    private static string Location(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();

        return $"{span.Path}:{span.StartLinePosition.Line + 1}";
    }

    private static bool IsBuildOutput(string sourceRoot, string path) =>
        Path.GetRelativePath(sourceRoot, path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Intersect(BuildOutputDirectories, StringComparer.OrdinalIgnoreCase)
            .Any();

    private static DirectoryInfo RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Trail.slnx")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException("The repository root (Trail.slnx) was not found.");
    }

    private sealed record UnresolvedCode(string Type, string Expression, string Location);

    private sealed record MessageCodeScan(SortedSet<string> Codes, IReadOnlyList<UnresolvedCode> Unresolved);
}
