namespace Trail.Application.Accounts;

public enum PasswordSignInResult
{
    Success,
    InvalidCredentials,
    LockedOut,
    EmailNotConfirmed,
    Deactivated,
}
