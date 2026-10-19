using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using Trail.Application.Accounts;
using Trail.Web.Accounts;

namespace Trail.Web.Tests.Accounts;

public sealed partial class AccountEmailContentTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    private const string Recipient = "student@example.com";
    private const string Link = "https://trail.example/Account/ConfirmEmail?userId=1&token=a+b/c=\"<x>";

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
