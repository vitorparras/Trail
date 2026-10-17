using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public interface IAccountIdentityGateway
{
    Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default);

    RegistrationMode GetRegistrationMode();

    Task<Result<UserId>> CreateUserAsync(
        string email,
        string password,
        bool isAdmin,
        bool emailConfirmed,
        CancellationToken cancellationToken = default);

    Task<Result> ConfirmEmailAsync(
        UserId userId,
        string token,
        CancellationToken cancellationToken = default);

    Task<Result<string>> GenerateEmailConfirmationTokenAsync(
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<PasswordSignInResult> SignInWithPasswordAsync(
        string email,
        string password,
        bool isPersistent,
        CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);

    Task<Result> ChangePasswordAsync(
        UserId userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<Result<string>> GeneratePasswordResetTokenAsync(
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(
        UserId userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountUserDto>> ListUsersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountInvitationDto>> ListInvitationsAsync(CancellationToken cancellationToken = default);

    Task<Result> SetUserActiveStatusAsync(
        UserId userId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<Result<AccountInvitationDto>> CreateInvitationAsync(
        string email,
        UserId invitedBy,
        CancellationToken cancellationToken = default);

    Task<Result<AccountInvitationDto>> ValidateInvitationTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<Result> MarkInvitationAcceptedAsync(
        string token,
        UserId acceptedBy,
        CancellationToken cancellationToken = default);

    Task<AccountUserDto?> GetUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<AccountUserDto?> GetUserByIdAsync(
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<Result> ResetAdminPasswordAsync(
        string newPassword,
        CancellationToken cancellationToken = default);
}
