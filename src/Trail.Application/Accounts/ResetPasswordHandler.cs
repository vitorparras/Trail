using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record ResetPasswordCommand(
    UserId UserId,
    string Token,
    string NewPassword,
    string ConfirmNewPassword);

public sealed class ResetPasswordHandler
{
    private readonly IAccountIdentityGateway _identityGateway;

    public ResetPasswordHandler(IAccountIdentityGateway identityGateway)
    {
        ArgumentNullException.ThrowIfNull(identityGateway);
        _identityGateway = identityGateway;
    }

    public async Task<Result> HandleAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var notification = new Notification();

        if (command.UserId.Value == Guid.Empty)
        {
            notification.Add(Message.Error("accounts.password_reset.invalid_user", nameof(command.UserId)));
        }

        if (string.IsNullOrWhiteSpace(command.Token))
        {
            notification.Add(Message.Error("accounts.password_reset.invalid_token", nameof(command.Token)));
        }

        if (string.IsNullOrWhiteSpace(command.NewPassword))
        {
            notification.Add(Message.Error("accounts.password_reset.password_required", nameof(command.NewPassword)));
        }
        else if (command.NewPassword != command.ConfirmNewPassword)
        {
            notification.Add(Message.Error("accounts.password_reset.passwords_do_not_match", nameof(command.ConfirmNewPassword)));
        }

        if (notification.HasErrors)
        {
            return notification.ToResult();
        }

        return await _identityGateway.ResetPasswordAsync(
            command.UserId,
            command.Token,
            command.NewPassword,
            cancellationToken);
    }
}
