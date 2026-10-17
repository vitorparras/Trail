using Trail.Application.Abstractions;
using Trail.Application.Telemetry;
using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record SendTestEmailCommand(string TargetEmail);

public sealed class SendTestEmailHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IEmailSender _emailSender;
    private readonly ApplicationMetrics _metrics;

    public SendTestEmailHandler(
        ICurrentUser currentUser,
        IEmailSender emailSender,
        ApplicationMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(emailSender);
        ArgumentNullException.ThrowIfNull(metrics);

        _currentUser = currentUser;
        _emailSender = emailSender;
        _metrics = metrics;
    }

    public async Task<Result> HandleAsync(
        SendTestEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
        {
            return Result.Forbidden(Message.Error("accounts.admin.forbidden"));
        }

        if (string.IsNullOrWhiteSpace(command.TargetEmail))
        {
            return Result.Invalid(Message.Error("accounts.test_email.email_required", nameof(command.TargetEmail)));
        }

        if (!_emailSender.IsSmtpConfigured)
        {
            return Result.Invalid(Message.Error("accounts.test_email.smtp_not_configured"));
        }

        var message = new EmailMessage(
            command.TargetEmail,
            "Trail - Test Email",
            "<p>This is a test email sent from Trail.</p>",
            "This is a test email sent from Trail.",
            EmailKinds.Test);

        var sendResult = await _emailSender.SendAsync(message, cancellationToken);
        if (!sendResult.IsOk)
        {
            _metrics.RecordEmailFailed(EmailKinds.Test);
            return sendResult;
        }

        _metrics.RecordEmailSent(EmailKinds.Test);
        return Result.Ok();
    }
}
