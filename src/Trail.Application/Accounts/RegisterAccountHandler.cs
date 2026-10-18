using Trail.Application.Telemetry;
using Trail.Domain.Accounts;
using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record RegisterAccountCommand(
    string Email,
    string Password,
    string ConfirmPassword,
    string? InviteToken = null,
    Func<UserId, string, string>? ConfirmationLinkBuilder = null);

public sealed class RegisterAccountHandler
{
    private readonly IAccountIdentityGateway _identityGateway;
    private readonly IUserSettingsStore _userSettingsStore;
    private readonly IEmailSender _emailSender;
    private readonly ApplicationMetrics _metrics;

    public RegisterAccountHandler(
        IAccountIdentityGateway identityGateway,
        IUserSettingsStore userSettingsStore,
        IEmailSender emailSender,
        ApplicationMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(identityGateway);
        ArgumentNullException.ThrowIfNull(userSettingsStore);
        ArgumentNullException.ThrowIfNull(emailSender);
        ArgumentNullException.ThrowIfNull(metrics);

        _identityGateway = identityGateway;
        _userSettingsStore = userSettingsStore;
        _emailSender = emailSender;
        _metrics = metrics;
    }

    public async Task<Result<RegisterAccountOutcome>> HandleAsync(
        RegisterAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var notification = new Notification();

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            notification.Add(Message.Error("accounts.registration.email_required", nameof(command.Email)));
        }
        else if (!IsValidEmail(command.Email))
        {
            notification.Add(Message.Error("accounts.registration.email_invalid", nameof(command.Email)));
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            notification.Add(Message.Error("accounts.registration.password_required", nameof(command.Password)));
        }
        else if (command.Password != command.ConfirmPassword)
        {
            notification.Add(Message.Error("accounts.registration.passwords_do_not_match", nameof(command.ConfirmPassword)));
        }

        if (notification.HasErrors)
        {
            return notification.ToResult<RegisterAccountOutcome>(() => throw new InvalidOperationException());
        }

        var anyUsers = await _identityGateway.AnyUsersExistAsync(cancellationToken);
        if (!anyUsers)
        {
            return await HandleFirstUserAsync(command, cancellationToken);
        }

        return await HandleSubsequentUserAsync(command, cancellationToken);
    }

    private async Task<Result<RegisterAccountOutcome>> HandleFirstUserAsync(
        RegisterAccountCommand command,
        CancellationToken cancellationToken)
    {
        var autoConfirm = !_emailSender.IsSmtpConfigured;
        var createResult = await _identityGateway.CreateUserAsync(
            command.Email,
            command.Password,
            isAdmin: true,
            emailConfirmed: autoConfirm,
            cancellationToken);

        if (!createResult.IsOk)
        {
            return createResult.Failure;
        }

        var userId = createResult.Data;
        var settings = UserSettings.CreateDefault(userId);
        await _userSettingsStore.AddAsync(settings, cancellationToken);
        await _userSettingsStore.SaveChangesAsync(cancellationToken);

        if (autoConfirm)
        {
            return Result.Ok(new RegisterAccountOutcome(RequiresEmailConfirmation: false, UserId: userId));
        }

        await SendConfirmationEmailAsync(userId, command.Email, command.ConfirmationLinkBuilder, cancellationToken);
        return Result.Ok(new RegisterAccountOutcome(RequiresEmailConfirmation: true, UserId: userId));
    }

    private async Task<Result<RegisterAccountOutcome>> HandleSubsequentUserAsync(
        RegisterAccountCommand command,
        CancellationToken cancellationToken)
    {
        var mode = _identityGateway.GetRegistrationMode();

        if (mode == RegistrationMode.Closed)
        {
            return Result.Forbidden(Message.Error("accounts.registration.closed"));
        }

        if (mode == RegistrationMode.InviteOnly)
        {
            if (string.IsNullOrWhiteSpace(command.InviteToken))
            {
                return Result.Forbidden(Message.Error("accounts.registration.invite_required"));
            }

            var inviteResult = await _identityGateway.ValidateInvitationTokenAsync(command.InviteToken, cancellationToken);
            if (!inviteResult.IsOk)
            {
                return Result.Invalid(Message.Error("accounts.registration.invite_invalid"));
            }

            if (!string.Equals(inviteResult.Data.Email, command.Email, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Invalid(Message.Error("accounts.registration.invite_invalid"));
            }
        }

        var passwordResult = await _identityGateway.ValidatePasswordAsync(command.Email, command.Password, cancellationToken);
        if (!passwordResult.IsOk)
        {
            return passwordResult.Failure;
        }

        var existingUser = await _identityGateway.GetUserByEmailAsync(command.Email, cancellationToken);
        if (existingUser is not null)
        {
            await _identityGateway.SimulateAccountCreationAsync(command.Email, command.Password, cancellationToken);
            return IndistinguishableFromNewAccount();
        }

        var createResult = await _identityGateway.CreateUserAsync(
            command.Email,
            command.Password,
            isAdmin: false,
            emailConfirmed: false,
            cancellationToken);

        if (createResult.Status is ResultStatus.Conflict)
        {
            return IndistinguishableFromNewAccount();
        }

        if (!createResult.IsOk)
        {
            return createResult.Failure;
        }

        var userId = createResult.Data;

        if (mode == RegistrationMode.InviteOnly)
        {
            await _identityGateway.MarkInvitationAcceptedAsync(command.InviteToken!, userId, cancellationToken);
        }

        var settings = UserSettings.CreateDefault(userId);
        await _userSettingsStore.AddAsync(settings, cancellationToken);
        await _userSettingsStore.SaveChangesAsync(cancellationToken);

        if (_emailSender.IsSmtpConfigured)
        {
            await SendConfirmationEmailAsync(userId, command.Email, command.ConfirmationLinkBuilder, cancellationToken);
        }

        return Result.Ok(new RegisterAccountOutcome(RequiresEmailConfirmation: true, UserId: userId));
    }

    private static Result<RegisterAccountOutcome> IndistinguishableFromNewAccount() =>
        Result.Ok(new RegisterAccountOutcome(RequiresEmailConfirmation: true));

    private async Task SendConfirmationEmailAsync(
        UserId userId,
        string email,
        Func<UserId, string, string>? linkBuilder,
        CancellationToken cancellationToken)
    {
        var tokenResult = await _identityGateway.GenerateEmailConfirmationTokenAsync(userId, cancellationToken);
        if (!tokenResult.IsOk)
        {
            return;
        }

        var confirmationUrl = linkBuilder?.Invoke(userId, tokenResult.Data)
            ?? $"/Account/ConfirmEmail?userId={userId}&token={Uri.EscapeDataString(tokenResult.Data)}";

        var message = new EmailMessage(
            email,
            "Confirm your account",
            $"<p>Please confirm your account by clicking <a href=\"{confirmationUrl}\">here</a>.</p>",
            $"Please confirm your account by visiting: {confirmationUrl}",
            EmailKinds.Confirmation);

        var sendResult = await _emailSender.SendAsync(message, cancellationToken);
        if (sendResult.IsOk)
        {
            _metrics.RecordEmailSent(EmailKinds.Confirmation);
        }
        else
        {
            _metrics.RecordEmailFailed(EmailKinds.Confirmation);
        }
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return email.Contains('@')
            && email.Contains('.')
            && !email.Contains(' ')
            && System.Net.Mail.MailAddress.TryCreate(email, out var address)
            && address.Address == email;
    }
}
