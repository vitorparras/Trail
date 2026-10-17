using Trail.Application.Abstractions;
using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword);

public sealed class ChangePasswordHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IAccountIdentityGateway _identityGateway;

    public ChangePasswordHandler(
        ICurrentUser currentUser,
        IAccountIdentityGateway identityGateway)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(identityGateway);

        _currentUser = currentUser;
        _identityGateway = identityGateway;
    }

    public async Task<Result> HandleAsync(
        ChangePasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.IsAuthenticated)
        {
            return Result.Forbidden(Message.Error("accounts.auth.unauthenticated"));
        }

        var notification = new Notification();

        if (string.IsNullOrWhiteSpace(command.CurrentPassword))
        {
            notification.Add(Message.Error("accounts.change_password.current_password_required", nameof(command.CurrentPassword)));
        }

        if (string.IsNullOrWhiteSpace(command.NewPassword))
        {
            notification.Add(Message.Error("accounts.change_password.new_password_required", nameof(command.NewPassword)));
        }
        else if (command.NewPassword != command.ConfirmNewPassword)
        {
            notification.Add(Message.Error("accounts.change_password.passwords_do_not_match", nameof(command.ConfirmNewPassword)));
        }

        if (notification.HasErrors)
        {
            return notification.ToResult();
        }

        return await _identityGateway.ChangePasswordAsync(
            _currentUser.Id,
            command.CurrentPassword,
            command.NewPassword,
            cancellationToken);
    }
}
