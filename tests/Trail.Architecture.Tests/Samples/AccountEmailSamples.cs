using Trail.Application.Accounts;

namespace Trail.Application.Violations.AccountEmails.TargetTyped
{
    public static class TargetTypedEmail
    {
        public static EmailMessage Build() => new("someone@example.com", "Subject", "<p>Body</p>", "Body", EmailKinds.Test);
    }
}

namespace Trail.Application.Violations.AccountEmails.FullyQualified
{
    public static class FullyQualifiedEmail
    {
        public static object Build() =>
            new Trail.Application.Accounts.EmailMessage("someone@example.com", "Subject", "<p>Body</p>", "Body", EmailKinds.Test);
    }
}

namespace Trail.Application.Violations.AccountEmails.WithExpression
{
    public static class RetargetedEmail
    {
        public static EmailMessage Retarget(EmailMessage message) => message with { ToAddress = "other@example.com" };
    }
}

namespace Trail.Application.Violations.AccountEmails.Lambda
{
    public static class LambdaEmail
    {
        public static Func<string, EmailMessage> Factory() =>
            address => new EmailMessage(address, "Subject", "<p>Body</p>", "Body", EmailKinds.Test);
    }
}

namespace Trail.Application.Violations.AccountEmails.AsyncMethod
{
    public static class AsyncEmail
    {
        public static async Task<EmailMessage> BuildAsync()
        {
            await Task.Yield();
            return new("someone@example.com", "Subject", "<p>Body</p>", "Body", EmailKinds.Test);
        }
    }
}

namespace Trail.Application.Violations.AccountEmails.Valid
{
    public static class EmailReader
    {
        public static string Describe(EmailMessage message) => string.Concat(message.Kind, " ", message.Subject);
    }
}
