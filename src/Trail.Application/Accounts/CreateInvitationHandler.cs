using Trail.Application.Abstractions;
using Trail.Application.Telemetry;
using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record CreateInvitationCommand(
    string Email,
    Func<string, string>? InvitationLinkBuilder = null);

public sealed class CreateInvitationHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IAccountIdentityGateway _identityGateway;
    private readonly IEmailSender _emailSender;
    private readonly ApplicationMetrics _metrics;

    public CreateInvitationHandler(
        ICurrentUser currentUser,
        IAccountIdentityGateway identityGateway,
        IEmailSender emailSender,
        ApplicationMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(identityGateway);
        ArgumentNullException.ThrowIfNull(emailSender);
        ArgumentNullException.ThrowIfNull(metrics);

        _currentUser = currentUser;
        _identityGateway = identityGateway;
        _emailSender = emailSender;
        _metrics = metrics;
    }

    public async Task<Result<CreateInvitationOutcome>> HandleAsync(
        CreateInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
        {
            return Result.Forbidden(Message.Error("accounts.admin.forbidden"));
        }

        if (_identityGateway.GetRegistrationMode() == RegistrationMode.Closed)
        {
            return Result.Forbidden(Message.Error("accounts.invitation.registration_closed"));
        }

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return Result.Invalid(Message.Error("accounts.invitation.email_required", nameof(command.Email)));
        }

        var existingUser = await _identityGateway.GetUserByEmailAsync(command.Email, cancellationToken);
        if (existingUser is not null)
        {
            return Result.Conflict(Message.Error("accounts.invitation.user_already_exists", nameof(command.Email)));
        }

        var createResult = await _identityGateway.CreateInvitationAsync(
            command.Email,
            _currentUser.Id,
            cancellationToken);

        if (!createResult.IsOk)
        {
            return createResult.Failure;
        }

        var invitation = createResult.Data;
        var link = command.InvitationLinkBuilder?.Invoke(invitation.Token)
            ?? $"/Account/Register?inviteToken={Uri.EscapeDataString(invitation.Token)}";

        var emailSent = false;
        if (_emailSender.IsSmtpConfigured)
        {
            var message = new EmailMessage(
                command.Email,
                "You have been invited to Trail",
                $"<p>You have been invited to Trail. Accept your invitation by clicking <a href=\"{link}\">here</a>.</p>",
                $"You have been invited to Trail. Accept your invitation by visiting: {link}",
                EmailKinds.Invitation);

            var sendResult = await _emailSender.SendAsync(message, cancellationToken);
            if (sendResult.IsOk)
            {
                _metrics.RecordEmailSent(EmailKinds.Invitation);
                emailSent = true;
            }
            else
            {
                _metrics.RecordEmailFailed(EmailKinds.Invitation);
                emailSent = false;
            }
        }

        return Result.Ok(new CreateInvitationOutcome(
            invitation.Email,
            invitation.Token,
            link,
            emailSent));
    }
}
