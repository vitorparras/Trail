using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record ConfirmEmailCommand(UserId UserId, string Token);

public sealed class ConfirmEmailHandler
{
    private readonly IAccountIdentityGateway _identityGateway;

    public ConfirmEmailHandler(IAccountIdentityGateway identityGateway)
    {
        ArgumentNullException.ThrowIfNull(identityGateway);
        _identityGateway = identityGateway;
    }

    public async Task<Result> HandleAsync(
        ConfirmEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.UserId.Value == Guid.Empty || string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Invalid(Message.Error("accounts.confirm_email.invalid_token"));
        }

        return await _identityGateway.ConfirmEmailAsync(command.UserId, command.Token, cancellationToken);
    }
}
