namespace Trail.Application.Accounts;

public sealed record EmailMessage(
    string ToAddress,
    string Subject,
    string HtmlBody,
    string TextBody,
    string Kind);

public static class EmailKinds
{
    public const string Confirmation = "Confirmation";
    public const string PasswordReset = "PasswordReset";
    public const string RegistrationAttempt = "RegistrationAttempt";
    public const string Invitation = "Invitation";
    public const string Test = "Test";
}
