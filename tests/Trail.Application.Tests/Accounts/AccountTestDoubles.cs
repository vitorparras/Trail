using Trail.Application.Abstractions;
using Trail.Application.Accounts;
using Trail.Domain.Accounts;
using Trail.Domain.Common;

namespace Trail.Application.Tests.Accounts;

internal sealed class TestCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; } = true;

    public UserId Id { get; set; } = UserId.New();

    public bool IsAdmin { get; set; }
}

internal sealed class FakeEmailSender : IEmailSender
{
    public bool IsSmtpConfigured { get; set; } = true;

    public List<EmailMessage> SentMessages { get; } = [];

    public Result SendResult { get; set; } = Result.Ok();

    public Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        SentMessages.Add(message);
        return Task.FromResult(SendResult);
    }
}

internal sealed class FakeUserSettingsStore : IUserSettingsStore
{
    public UserSettings? Settings { get; set; }

    public bool Saved { get; private set; }

    public Task<UserSettings?> GetForCurrentUserAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Settings);

    public Task AddAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        Settings = settings;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saved = true;
        return Task.CompletedTask;
    }
}

internal sealed class FakeAccountIdentityGateway : IAccountIdentityGateway
{
    public bool AnyUsersExistResult { get; set; } = true;

    public RegistrationMode RegistrationMode { get; set; } = RegistrationMode.Open;

    public List<AccountUserDto> Users { get; } = [];

    public List<AccountInvitationDto> Invitations { get; } = [];

    public Result<UserId>? CreateUserResult { get; set; }

    public Result ConfirmEmailResult { get; set; } = Result.Ok();

    public Result<string> EmailConfirmationTokenResult { get; set; } = Result.Ok("sample-confirm-token");

    public PasswordSignInResult SignInResult { get; set; } = PasswordSignInResult.Success;

    public bool SignedOut { get; private set; }

    public Result ChangePasswordResult { get; set; } = Result.Ok();

    public Result<string> PasswordResetTokenResult { get; set; } = Result.Ok("sample-reset-token");

    public Result ResetPasswordResult { get; set; } = Result.Ok();

    public Result SetUserActiveStatusResult { get; set; } = Result.Ok();

    public Result<AccountInvitationDto>? CreateInvitationResult { get; set; }

    public Result<AccountInvitationDto>? ValidateInvitationResult { get; set; }

    public Result MarkInvitationAcceptedResult { get; set; } = Result.Ok();

    public string? LastAcceptedToken { get; private set; }

    public UserId? LastAcceptedBy { get; private set; }

    public Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(AnyUsersExistResult);

    public RegistrationMode GetRegistrationMode() => RegistrationMode;

    public Task<Result<UserId>> CreateUserAsync(
        string email,
        string password,
        bool isAdmin,
        bool emailConfirmed,
        CancellationToken cancellationToken = default)
    {
        if (CreateUserResult is not null)
        {
            return Task.FromResult(CreateUserResult);
        }

        var newUserId = UserId.New();
        var user = new AccountUserDto(newUserId, email, isAdmin, true, emailConfirmed, DateTime.UtcNow);
        Users.Add(user);
        return Task.FromResult(Result.Ok(newUserId));
    }

    public Task<Result> ConfirmEmailAsync(UserId userId, string token, CancellationToken cancellationToken = default) =>
        Task.FromResult(ConfirmEmailResult);

    public Task<Result<string>> GenerateEmailConfirmationTokenAsync(UserId userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(EmailConfirmationTokenResult);

    public Task<PasswordSignInResult> SignInWithPasswordAsync(
        string email,
        string password,
        bool isPersistent,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SignInResult);

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        SignedOut = true;
        return Task.CompletedTask;
    }

    public Task<Result> ChangePasswordAsync(
        UserId userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ChangePasswordResult);

    public Task<Result<string>> GeneratePasswordResetTokenAsync(UserId userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PasswordResetTokenResult);

    public Task<Result> ResetPasswordAsync(
        UserId userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ResetPasswordResult);

    public Task<IReadOnlyList<AccountUserDto>> ListUsersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AccountUserDto>>(Users);

    public Task<IReadOnlyList<AccountInvitationDto>> ListInvitationsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AccountInvitationDto>>(Invitations);

    public Task<Result> SetUserActiveStatusAsync(
        UserId userId,
        bool isActive,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SetUserActiveStatusResult);

    public Task<Result<AccountInvitationDto>> CreateInvitationAsync(
        string email,
        UserId invitedBy,
        CancellationToken cancellationToken = default)
    {
        if (CreateInvitationResult is not null)
        {
            return Task.FromResult(CreateInvitationResult);
        }

        var invitation = new AccountInvitationDto(
            Guid.NewGuid(),
            email,
            "sample-invite-token",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            false,
            null);
        Invitations.Add(invitation);
        return Task.FromResult(Result.Ok(invitation));
    }

    public Task<Result<AccountInvitationDto>> ValidateInvitationTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (ValidateInvitationResult is not null)
        {
            return Task.FromResult(ValidateInvitationResult);
        }

        var found = Invitations.FirstOrDefault(i => i.Token == token);
        if (found is null)
        {
            return Task.FromResult<Result<AccountInvitationDto>>(Result.NotFound(Message.Error("invitation.not_found")));
        }

        return Task.FromResult(Result.Ok(found));
    }

    public Task<Result> MarkInvitationAcceptedAsync(string token, UserId acceptedBy, CancellationToken cancellationToken = default)
    {
        LastAcceptedToken = token;
        LastAcceptedBy = acceptedBy;
        return Task.FromResult(MarkInvitationAcceptedResult);
    }

    public Task<AccountUserDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<AccountUserDto?> GetUserByIdAsync(UserId userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));
}
