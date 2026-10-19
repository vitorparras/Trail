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
    private const string CountedCodeFactoryType = "DeletionImpactItem";
    private const string CountedCodeFactoryMethod = "Create";

    private static readonly HashSet<(string Type, string Expression)> AllowedComputedCodes =
    [
        ("Notification", "code"),
        ("IdentityErrorMessages", "CodeFor(error)"),
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

    [Fact]
    public void CountedMessageCodes_InProductionCode_HaveTheOneAndOtherFormsInTheResourceOfTheirPrefix()
    {
        var localizerFactory = factory.Services.GetRequiredService<IStringLocalizerFactory>();
        var codes = Scan.Value.CountedCodes;

        Assert.Contains("planning.deletion.topics", codes);
        Assert.Empty(MissingCountedForms(localizerFactory, codes));
    }

    [Fact]
    public void MissingCountedForms_WhenAFormIsMissing_ReportsTheKeyAndItsResource()
    {
        var missing = MissingCountedForms(new OneFormOnlyLocalizerFactory(), ["practice.sample.entries", "planning.sample.items"]);

        Assert.Equal(
            [$"practice.sample.entries.other ({nameof(PracticeResource)}.resx)", $"planning.sample.items.other ({nameof(SharedResource)}.resx)"],
            missing);
    }

    [Fact]
    public void ScanRoots_DeletionImpactItems_CollectsTheirCodesAsCountedAndFlagsComputedOnes()
    {
        const string source = """
            public sealed class SampleContributor
            {
                private const string EntriesCode = "practice.sample.entries";

                public object Literal() => DeletionImpactItem.Create("fitness.sample.attempts", 1);

                public object FromConstant() => DeletionImpactItem.Create(EntriesCode, 2);

                public object Named() => DeletionImpactItem.Create(count: 3, code: "studytime.sample.sessions");

                public object Computed(string code) => DeletionImpactItem.Create(code, 4);

                public object Text() => Message.Error("planning.sample.message");
            }
            """;

        var scan = ScanRoots([CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken)]);

        Assert.Equal(["fitness.sample.attempts", "practice.sample.entries", "studytime.sample.sessions"], scan.CountedCodes);
        Assert.Equal(["planning.sample.message"], scan.Codes);
        var computed = Assert.Single(scan.Unresolved);
        Assert.Equal(("SampleContributor", "code"), (computed.Type, computed.Expression));
    }

    private static List<string> MissingCountedForms(IStringLocalizerFactory localizerFactory, IEnumerable<string> codes) =>
        [.. codes.SelectMany(code => CountedMessage.KeysFor(code)
            .Where(key => localizerFactory.CreateForCode(code)[key].ResourceNotFound)
            .Select(key => $"{key} ({MessageResourceRegistry.ResourceTypeFor(code).Name}.resx)"))];

    private static MessageCodeScan ScanSources()
    {
        var sourceRoot = Path.Combine(RepositoryRoot().FullName, "src");
        var roots = ScannedProjects
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(sourceRoot, project), "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(sourceRoot, path))
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: Path.GetRelativePath(sourceRoot, path)).GetRoot())
            .ToList();

        return ScanRoots(roots);
    }

    private static MessageCodeScan ScanRoots(IReadOnlyList<SyntaxNode> roots)
    {
        var constants = StringConstants(roots);

        var codes = new SortedSet<string>(StringComparer.Ordinal);
        var countedCodes = new SortedSet<string>(StringComparer.Ordinal);
        var unresolved = new List<UnresolvedCode>();

        foreach (var (argument, counted) in roots.SelectMany(root => root.DescendantNodes().OfType<InvocationExpressionSyntax>()).Select(CodeArgument).OfType<CodeArgumentMatch>())
        {
            var containingType = ContainingTypeName(argument);
            var resolved = Resolve(argument.Expression, containingType, constants);
            if (resolved is null)
            {
                unresolved.Add(new UnresolvedCode(containingType, argument.Expression.ToString(), Location(argument)));
            }
            else
            {
                (counted ? countedCodes : codes).UnionWith(resolved);
            }
        }

        return new MessageCodeScan(codes, countedCodes, unresolved);
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

    private static CodeArgumentMatch? CodeArgument(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: var method } access
                when IsMessageType(access.Expression) && MessageFactoryMethods.Contains(method) => Match(Argument(invocation, position: 0), counted: false),
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: CountedCodeFactoryMethod } access
                when SimpleName(access.Expression) == CountedCodeFactoryType => Match(Argument(invocation, position: 0), counted: true),
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "AddErrorIf" } => Match(Argument(invocation, position: 1), counted: false),
            IdentifierNameSyntax { Identifier.ValueText: "AddErrorIf" } => Match(Argument(invocation, position: 1), counted: false),
            _ => null,
        };

    private static CodeArgumentMatch? Match(ArgumentSyntax? argument, bool counted) =>
        argument is null ? null : new CodeArgumentMatch(argument, counted);

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

    private sealed record CodeArgumentMatch(ArgumentSyntax Argument, bool Counted);

    private sealed record MessageCodeScan(SortedSet<string> Codes, SortedSet<string> CountedCodes, IReadOnlyList<UnresolvedCode> Unresolved);

    private sealed class OneFormOnlyLocalizerFactory : IStringLocalizerFactory
    {
        public IStringLocalizer Create(Type resourceSource) => new OneFormOnlyLocalizer();

        public IStringLocalizer Create(string baseName, string location) => new OneFormOnlyLocalizer();
    }

    private sealed class OneFormOnlyLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] =>
            new(name, name, resourceNotFound: !name.EndsWith(CountedMessage.OneSuffix, StringComparison.Ordinal));

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
