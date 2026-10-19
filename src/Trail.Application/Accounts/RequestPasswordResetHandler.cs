using Trail.Application.Telemetry;
using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record RequestPasswordResetCommand(
    string Email,
    Func<UserId, string, string>? ResetLinkBuilder = null);

public sealed class RequestPasswordResetHandler
{
    private readonly IAccountIdentityGateway _identityGateway;
    private readonly IEmailSender _emailSender;
    private readonly IAccountEmailContent _emailContent;
    private readonly ApplicationMetrics _metrics;

    public RequestPasswordResetHandler(
        IAccountIdentityGateway identityGateway,
        IEmailSender emailSender,
        IAccountEmailContent emailContent,
        ApplicationMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(identityGateway);
        ArgumentNullException.ThrowIfNull(emailSender);
        ArgumentNullException.ThrowIfNull(emailContent);
        ArgumentNullException.ThrowIfNull(metrics);

        _identityGateway = identityGateway;
        _emailSender = emailSender;
        _emailContent = emailContent;
        _metrics = metrics;
    }

    public async Task<Result> HandleAsync(
        RequestPasswordResetCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return Result.Invalid(Message.Error("accounts.password_reset.email_required", nameof(command.Email)));
        }

        var user = await _identityGateway.GetUserByEmailAsync(command.Email, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Ok();
        }

        if (!_emailSender.IsSmtpConfigured)
        {
            return Result.Ok();
        }

        var tokenResult = await _identityGateway.GeneratePasswordResetTokenAsync(user.Id, cancellationToken);
        if (!tokenResult.IsOk)
        {
            return Result.Ok();
        }

        var resetUrl = command.ResetLinkBuilder?.Invoke(user.Id, tokenResult.Data)
            ?? $"/Account/ResetPassword?userId={user.Id}&token={Uri.EscapeDataString(tokenResult.Data)}";

        var message = _emailContent.PasswordReset(command.Email, resetUrl);

        var sendResult = await _emailSender.SendAsync(message, cancellationToken);
        if (sendResult.IsOk)
        {
            _metrics.RecordEmailSent(EmailKinds.PasswordReset);
        }
        else
        {
            _metrics.RecordEmailFailed(EmailKinds.PasswordReset);
        }

        return Result.Ok();
    }
}
