using System.Net;
using Microsoft.Extensions.Localization;
using Trail.Application.Accounts;

namespace Trail.Web.Accounts;

public sealed class LocalizedAccountEmailContent : IAccountEmailContent
{
    private const string ConfirmationPrefix = "accounts.email.confirmation";
    private const string PasswordResetPrefix = "accounts.email.password_reset";
    private const string InvitationPrefix = "accounts.email.invitation";
    private const string TestPrefix = "accounts.email.test";
    private const string ParagraphBreak = "\n\n";

    private readonly IStringLocalizer<SharedResource> _localizer;

    public LocalizedAccountEmailContent(IStringLocalizer<SharedResource> localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        _localizer = localizer;
    }

    public EmailMessage EmailConfirmation(string toAddress, string confirmationLink) =>
        WithLink(toAddress, EmailKinds.Confirmation, ConfirmationPrefix, confirmationLink);

    public EmailMessage PasswordReset(string toAddress, string resetLink) =>
        WithLink(toAddress, EmailKinds.PasswordReset, PasswordResetPrefix, resetLink);

    public EmailMessage Invitation(string toAddress, string invitationLink) =>
        WithLink(toAddress, EmailKinds.Invitation, InvitationPrefix, invitationLink);

    public EmailMessage Test(string toAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toAddress);

        var body = Text(TestPrefix, "body");

        return new EmailMessage(toAddress, Text(TestPrefix, "subject"), Paragraph(body), body, EmailKinds.Test);
    }

    private EmailMessage WithLink(string toAddress, string kind, string prefix, string link)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(link);

        var body = Text(prefix, "body");
        var action = Text(prefix, "action");
        var footer = Text(prefix, "footer");

        var html = string.Concat(
            Paragraph(body),
            "<p><a href=\"", WebUtility.HtmlEncode(link), "\">", WebUtility.HtmlEncode(action), "</a></p>",
            Paragraph(footer));
        var text = string.Concat(body, ParagraphBreak, action, ": ", link, ParagraphBreak, footer);

        return new EmailMessage(toAddress, Text(prefix, "subject"), html, text, kind);
    }

    private string Text(string prefix, string part) => _localizer[string.Concat(prefix, ".", part)].Value;

    private static string Paragraph(string text) => string.Concat("<p>", WebUtility.HtmlEncode(text), "</p>");
}
