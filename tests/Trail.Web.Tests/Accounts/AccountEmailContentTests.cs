using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Localization;
using Trail.Application.Accounts;
using Trail.Web.Accounts;

namespace Trail.Web.Tests.Accounts;

public sealed partial class AccountEmailContentTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    private const string Recipient = "student@example.com";
    private const string Link = "https://trail.example/Account/ConfirmEmail?userId=1&token=a+b/c=\"<x>";

    private static readonly string[] EmailContentTypes = ["LocalizedAccountEmailContent", "FallbackAccountEmailContent"];
    private static readonly string[] ScannedProjects = ["Trail.Domain", "Trail.Application", "Trail.Infrastructure", "Trail.Web"];
    private static readonly string[] BuildOutputDirectories = ["bin", "obj"];

    public static TheoryData<string> Kinds =>
        [EmailKinds.Confirmation, EmailKinds.PasswordReset, EmailKinds.RegistrationAttempt, EmailKinds.Invitation, EmailKinds.Test];

    [Fact]
    public void Composition_RegistersTheLocalizedAccountEmailContent()
    {
        Assert.IsType<LocalizedAccountEmailContent>(factory.Services.GetRequiredService<IAccountEmailContent>());
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void AccountEmail_TakesTheSubjectAndBodyFromTheSharedResource(string kind)
    {
        var localizer = new RecordingLocalizer(factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>());

        var message = Compose(new LocalizedAccountEmailContent(localizer), kind);

        Assert.Equal(kind, message.Kind);
        Assert.Equal(Recipient, message.ToAddress);
        Assert.NotEmpty(localizer.Requested);
        Assert.All(localizer.Requested, text => Assert.False(text.ResourceNotFound, $"{text.Name} is missing from SharedResource.resx"));
        Assert.Equal(localizer.Requested.Single(text => text.Name.EndsWith(".subject", StringComparison.Ordinal)).Value, message.Subject);
        Assert.All(localizer.Requested.Where(text => !text.Name.EndsWith(".subject", StringComparison.Ordinal)), text =>
        {
            Assert.Contains(text.Value, message.TextBody, StringComparison.Ordinal);
            Assert.Contains(System.Net.WebUtility.HtmlEncode(text.Value), message.HtmlBody, StringComparison.Ordinal);
        });
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void AccountEmail_UsesNoTextOutsideTheResources(string kind)
    {
        var localizer = new KeyEchoLocalizer();

        var message = Compose(new LocalizedAccountEmailContent(localizer), kind);

        var textBody = message.TextBody.Replace(Link, string.Empty, StringComparison.Ordinal);
        var htmlBody = System.Net.WebUtility.HtmlDecode(Tags().Replace(message.HtmlBody, " "));
        Assert.StartsWith("accounts.email.", message.Subject, StringComparison.Ordinal);
        Assert.All(Words(textBody), word => Assert.StartsWith("accounts.email.", word, StringComparison.Ordinal));
        Assert.All(Words(htmlBody), word => Assert.StartsWith("accounts.email.", word, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(EmailKinds.Confirmation)]
    [InlineData(EmailKinds.PasswordReset)]
    [InlineData(EmailKinds.RegistrationAttempt)]
    [InlineData(EmailKinds.Invitation)]
    public void AccountEmail_EncodesTheLinkInTheHtmlBodyAndKeepsItInTheTextBody(string kind)
    {
        var content = factory.Services.GetRequiredService<IAccountEmailContent>();

        var message = Compose(content, kind);

        Assert.Contains($"href=\"{System.Net.WebUtility.HtmlEncode(Link)}\"", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<x>", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(Link, message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionCode_CreatesEmailMessagesOnlyInTheAccountEmailContent()
    {
        var sourceRoot = Path.Combine(RepositoryRoot().FullName, "src");
        var creators = ScannedProjects
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(sourceRoot, project), "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(sourceRoot, path))
            .SelectMany(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), cancellationToken: TestContext.Current.CancellationToken)
                .GetRoot(TestContext.Current.CancellationToken)
                .DescendantNodes()
                .Where(IsEmailMessageCreation)
                .Select(node => node.Ancestors().OfType<BaseTypeDeclarationSyntax>().First().Identifier.ValueText))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(EmailContentTypes.Order(StringComparer.Ordinal), creators.Order(StringComparer.Ordinal));
    }

    private static EmailMessage Compose(IAccountEmailContent content, string kind) =>
        kind switch
        {
            EmailKinds.Confirmation => content.EmailConfirmation(Recipient, Link),
            EmailKinds.PasswordReset => content.PasswordReset(Recipient, Link),
            EmailKinds.RegistrationAttempt => content.RegistrationAttempt(Recipient, Link),
            EmailKinds.Invitation => content.Invitation(Recipient, Link),
            EmailKinds.Test => content.Test(Recipient),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private static string[] Words(string text) =>
        text.Split([' ', '\n', '\r', ':'], StringSplitOptions.RemoveEmptyEntries);

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();

    private static bool IsEmailMessageCreation(SyntaxNode node) =>
        node switch
        {
            ObjectCreationExpressionSyntax creation => creation.Type.ToString() == nameof(EmailMessage),
            ImplicitObjectCreationExpressionSyntax implicitCreation => ReturnsEmailMessage(implicitCreation),
            _ => false,
        };

    private static bool ReturnsEmailMessage(SyntaxNode node) =>
        node.Ancestors().FirstOrDefault(ancestor => ancestor is MethodDeclarationSyntax or LocalDeclarationStatementSyntax or PropertyDeclarationSyntax) switch
        {
            MethodDeclarationSyntax method => method.ReturnType.ToString() == nameof(EmailMessage),
            LocalDeclarationStatementSyntax local => local.Declaration.Type.ToString() == nameof(EmailMessage),
            PropertyDeclarationSyntax property => property.Type.ToString() == nameof(EmailMessage),
            _ => false,
        };

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

    private sealed class RecordingLocalizer(IStringLocalizer<SharedResource> inner) : IStringLocalizer<SharedResource>
    {
        public List<LocalizedString> Requested { get; } = [];

        public LocalizedString this[string name] => Record(inner[name]);

        public LocalizedString this[string name, params object[] arguments] => Record(inner[name, arguments]);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => inner.GetAllStrings(includeParentCultures);

        private LocalizedString Record(LocalizedString text)
        {
            Requested.Add(text);
            return text;
        }
    }

    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
