using Trail.Domain.Accounts;
using Trail.Domain.Common;

namespace Trail.Application.Accounts;

internal sealed class FallbackAccountIdentityGateway : IAccountIdentityGateway
{
    public Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public RegistrationMode GetRegistrationMode() =>
        throw new NotImplementedException();

    public Task<Result<UserId>> CreateUserAsync(
        string email,
        string password,
        bool isAdmin,
        bool emailConfirmed,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Result> ConfirmEmailAsync(UserId userId, string token, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Result<string>> GenerateEmailConfirmationTokenAsync(UserId userId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<PasswordSignInResult> SignInWithPasswordAsync(
        string email,
        string password,
        bool isPersistent,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task SignOutAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Result> ChangePasswordAsync(
        UserId userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Result<string>> GeneratePasswordResetTokenAsync(UserId userId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Result> ResetPasswordAsync(
        UserId userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<AccountUserDto>> ListUsersAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<AccountInvitationDto>> ListInvitationsAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Result> SetUserActiveStatusAsync(UserId userId, bool isActive, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Result<AccountInvitationDto>> CreateInvitationAsync(
        string email,
        UserId invitedBy,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Result<AccountInvitationDto>> ValidateInvitationTokenAsync(string token, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Result> MarkInvitationAcceptedAsync(string token, UserId acceptedBy, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<AccountUserDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<AccountUserDto?> GetUserByIdAsync(UserId userId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Result> ResetAdminPasswordAsync(string newPassword, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}

internal sealed class FallbackEmailSender : IEmailSender
{
    public bool IsSmtpConfigured => false;

    public Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Ok());
}

internal sealed class FallbackUserSettingsStore : IUserSettingsStore
{
    public Task<UserSettings?> GetForCurrentUserAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<UserSettings?>(null);

    public Task AddAsync(UserSettings settings, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
