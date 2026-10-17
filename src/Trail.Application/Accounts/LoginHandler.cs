using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record LoginCommand(
    string Email,
    string Password,
    bool RememberMe = false);

public sealed class LoginHandler
{
    private readonly IAccountIdentityGateway _identityGateway;

    public LoginHandler(IAccountIdentityGateway identityGateway)
    {
        ArgumentNullException.ThrowIfNull(identityGateway);
        _identityGateway = identityGateway;
    }

    public async Task<Result> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var notification = new Notification();

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            notification.Add(Message.Error("accounts.login.email_required", nameof(command.Email)));
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            notification.Add(Message.Error("accounts.login.password_required", nameof(command.Password)));
        }

        if (notification.HasErrors)
        {
            return notification.ToResult();
        }

        var outcome = await _identityGateway.SignInWithPasswordAsync(
            command.Email,
            command.Password,
            command.RememberMe,
            cancellationToken);

        return outcome switch
        {
            PasswordSignInResult.Success => Result.Ok(),
            PasswordSignInResult.InvalidCredentials => Result.Invalid(Message.Error("accounts.login.invalid_credentials")),
            PasswordSignInResult.LockedOut => Result.Forbidden(Message.Error("accounts.login.locked_out")),
            PasswordSignInResult.EmailNotConfirmed => Result.Forbidden(Message.Error("accounts.login.email_not_confirmed")),
            PasswordSignInResult.Deactivated => Result.Forbidden(Message.Error("accounts.login.deactivated")),
            _ => Result.Invalid(Message.Error("accounts.login.invalid_credentials")),
        };
    }
}
