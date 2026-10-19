using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record ResendEmailConfirmationCommand(
    string Email,
    Func<UserId, string, string>? ConfirmationLinkBuilder = null);

public sealed class ResendEmailConfirmationHandler
{
    private readonly IAccountIdentityGateway _identityGateway;
    private readonly IEmailSender _emailSender;
    private readonly IAccountEmailContent _emailContent;
    private readonly IAccountEmailDelivery _emailDelivery;

    public ResendEmailConfirmationHandler(
        IAccountIdentityGateway identityGateway,
        IEmailSender emailSender,
        IAccountEmailContent emailContent,
        IAccountEmailDelivery emailDelivery)
    {
        ArgumentNullException.ThrowIfNull(identityGateway);
        ArgumentNullException.ThrowIfNull(emailSender);
        ArgumentNullException.ThrowIfNull(emailContent);
        ArgumentNullException.ThrowIfNull(emailDelivery);

        _identityGateway = identityGateway;
        _emailSender = emailSender;
        _emailContent = emailContent;
        _emailDelivery = emailDelivery;
    }

    public async Task<Result> HandleAsync(
        ResendEmailConfirmationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return Result.Invalid(Message.Error("accounts.resend_confirmation.email_required", nameof(command.Email)));
        }

        var user = await _identityGateway.GetUserByEmailAsync(command.Email, cancellationToken);
        if (user is null || user.EmailConfirmed || !user.IsActive)
        {
            return Result.Ok();
        }

        if (!_emailSender.IsSmtpConfigured)
        {
            return Result.Ok();
        }

        var tokenResult = await _identityGateway.GenerateEmailConfirmationTokenAsync(user.Id, cancellationToken);
        if (!tokenResult.IsOk)
        {
            return Result.Ok();
        }

        var confirmationUrl = command.ConfirmationLinkBuilder?.Invoke(user.Id, tokenResult.Data)
            ?? $"/Account/ConfirmEmail?userId={user.Id}&token={Uri.EscapeDataString(tokenResult.Data)}";

        _emailDelivery.Schedule(_emailContent.EmailConfirmation(command.Email, confirmationUrl));

        return Result.Ok();
    }
}
