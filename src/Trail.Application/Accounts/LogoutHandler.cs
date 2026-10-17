using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record LogoutCommand();

public sealed class LogoutHandler
{
    private readonly IAccountIdentityGateway _identityGateway;

    public LogoutHandler(IAccountIdentityGateway identityGateway)
    {
        ArgumentNullException.ThrowIfNull(identityGateway);
        _identityGateway = identityGateway;
    }

    public async Task<Result> HandleAsync(
        LogoutCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _identityGateway.SignOutAsync(cancellationToken);
        return Result.Ok();
    }
}
