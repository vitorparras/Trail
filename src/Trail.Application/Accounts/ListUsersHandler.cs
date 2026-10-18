using Trail.Application.Abstractions;
using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record ListUsersQuery();

public sealed class ListUsersHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IAccountIdentityGateway _identityGateway;

    public ListUsersHandler(
        ICurrentUser currentUser,
        IAccountIdentityGateway identityGateway)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(identityGateway);

        _currentUser = currentUser;
        _identityGateway = identityGateway;
    }

    public async Task<Result<AdminUsersOverviewDto>> HandleAsync(
        ListUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
        {
            return Result.Forbidden(Message.Error("accounts.admin.forbidden"));
        }

        var users = await _identityGateway.ListUsersAsync(cancellationToken);
        var invitations = await _identityGateway.ListInvitationsAsync(cancellationToken);

        return Result.Ok(new AdminUsersOverviewDto(users, invitations, _identityGateway.GetRegistrationMode()));
    }
}
