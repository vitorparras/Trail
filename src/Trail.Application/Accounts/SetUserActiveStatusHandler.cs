using Trail.Application.Abstractions;
using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record SetUserActiveStatusCommand(
    UserId TargetUserId,
    bool IsActive);

public sealed class SetUserActiveStatusHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IAccountIdentityGateway _identityGateway;

    public SetUserActiveStatusHandler(
        ICurrentUser currentUser,
        IAccountIdentityGateway identityGateway)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(identityGateway);

        _currentUser = currentUser;
        _identityGateway = identityGateway;
    }

    public async Task<Result> HandleAsync(
        SetUserActiveStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
        {
            return Result.Forbidden(Message.Error("accounts.admin.forbidden"));
        }

        if (command.TargetUserId == _currentUser.Id && !command.IsActive)
        {
            return Result.Invalid(Message.Error("accounts.admin.cannot_deactivate_self", nameof(command.TargetUserId)));
        }

        return await _identityGateway.SetUserActiveStatusAsync(
            command.TargetUserId,
            command.IsActive,
            cancellationToken);
    }
}
