using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trail.Application.Accounts;
using Trail.Domain.Common;
using Trail.Infrastructure.Persistence;

namespace Trail.Infrastructure.Identity;

public sealed class AccountIdentityGateway : IAccountIdentityGateway
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _dbContext;
    private readonly IOptions<RegistrationOptions> _registrationOptions;
    private readonly TimeProvider _timeProvider;

    public AccountIdentityGateway(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IHttpContextAccessor httpContextAccessor,
        AppDbContext dbContext,
        IOptions<RegistrationOptions> registrationOptions,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(signInManager);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(registrationOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _userManager = userManager;
        _signInManager = signInManager;
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
        _registrationOptions = registrationOptions;
        _timeProvider = timeProvider;
    }

    public async Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default)
    {
        return await _userManager.Users.AnyAsync(cancellationToken);
    }

    public RegistrationMode GetRegistrationMode()
    {
        return _registrationOptions.Value.Mode;
    }

    public async Task<Result<UserId>> CreateUserAsync(
        string email,
        string password,
        bool isAdmin,
        bool emailConfirmed,
        CancellationToken cancellationToken = default)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return EmailAlreadyExists();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            IsAdmin = isAdmin,
            IsActive = true,
            EmailConfirmed = emailConfirmed,
            CreatedAt = now,
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (createResult.Errors.Any(IdentityErrorMessages.IsDuplicateAccount))
        {
            return EmailAlreadyExists();
        }

        if (!createResult.Succeeded)
        {
            return Result.Invalid(IdentityErrorMessages.ToMessages(
                createResult.Errors,
                new IdentityErrorTargets(
                    Password: nameof(RegisterAccountCommand.Password),
                    Email: nameof(RegisterAccountCommand.Email))));
        }

        return Result.Ok(UserId.From(user.Id));
    }

    public async Task<Result> ValidatePasswordAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var candidate = new ApplicationUser { UserName = email, Email = email };
        var errors = new List<IdentityError>();
        foreach (var validator in _userManager.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(_userManager, candidate, password);
            errors.AddRange(validation.Errors);
        }

        return errors.Count == 0
            ? Result.Ok()
            : Result.Invalid(IdentityErrorMessages.ToMessages(
                errors,
                new IdentityErrorTargets(Password: nameof(RegisterAccountCommand.Password))));
    }

    public Task SimulateAccountCreationAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        _userManager.PasswordHasher.HashPassword(new ApplicationUser { UserName = email, Email = email }, password);
        return Task.CompletedTask;
    }

    public async Task<Result> ConfirmEmailAsync(
        UserId userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            return Result.Invalid(Message.Error("accounts.confirmation.invalid_token"));
        }

        var confirmResult = await _userManager.ConfirmEmailAsync(user, token);
        if (!confirmResult.Succeeded)
        {
            return Result.Invalid(Message.Error("accounts.confirmation.invalid_token"));
        }

        return Result.Ok();
    }

    public async Task<Result<string>> GenerateEmailConfirmationTokenAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            return Result.NotFound(Message.Error("accounts.confirmation.user_not_found"));
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        return Result.Ok(token);
    }

    public async Task<PasswordSignInResult> SignInWithPasswordAsync(
        string email,
        string password,
        bool isPersistent,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return PasswordSignInResult.InvalidCredentials;
        }

        if (!user.IsActive)
        {
            return PasswordSignInResult.Deactivated;
        }

        if (!user.EmailConfirmed)
        {
            return PasswordSignInResult.EmailNotConfirmed;
        }

        var checkResult = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (checkResult.IsLockedOut)
        {
            return PasswordSignInResult.LockedOut;
        }

        if (!checkResult.Succeeded)
        {
            return PasswordSignInResult.InvalidCredentials;
        }

        var extraClaims = new List<Claim>();
        if (user.IsAdmin)
        {
            extraClaims.Add(new Claim("trail:admin", "true"));
        }

        if (_httpContextAccessor.HttpContext is not null)
        {
            await _signInManager.SignInWithClaimsAsync(user, isPersistent, extraClaims);
        }

        return PasswordSignInResult.Success;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        if (_httpContextAccessor.HttpContext is not null)
        {
            await _signInManager.SignOutAsync();
        }
    }

    public async Task<Result> ChangePasswordAsync(
        UserId userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            return Result.NotFound(Message.Error("accounts.change_password.user_not_found"));
        }

        var changeResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!changeResult.Succeeded)
        {
            return Result.Invalid(IdentityErrorMessages.ToMessages(
                changeResult.Errors,
                new IdentityErrorTargets(
                    Password: nameof(ChangePasswordCommand.NewPassword),
                    CurrentPassword: nameof(ChangePasswordCommand.CurrentPassword))));
        }

        if (_httpContextAccessor.HttpContext is not null)
        {
            await _signInManager.RefreshSignInAsync(user);
        }

        return Result.Ok();
    }

    public async Task<Result<string>> GeneratePasswordResetTokenAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            return Result.NotFound(Message.Error("accounts.reset_password.user_not_found"));
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        return Result.Ok(token);
    }

    public async Task<Result> ResetPasswordAsync(
        UserId userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            return Result.Invalid(Message.Error("accounts.reset_password.invalid_token"));
        }

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            return result.Errors.Any(IdentityErrorMessages.IsInvalidToken)
                ? Result.Invalid(Message.Error("accounts.reset_password.invalid_token"))
                : Result.Invalid(IdentityErrorMessages.ToMessages(
                    result.Errors,
                    new IdentityErrorTargets(Password: nameof(ResetPasswordCommand.NewPassword))));
        }

        return Result.Ok();
    }

    public async Task<IReadOnlyList<AccountUserDto>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.CreatedAt)
            .Select(u => new AccountUserDto(
                UserId.From(u.Id),
                u.Email ?? string.Empty,
                u.IsAdmin,
                u.IsActive,
                u.EmailConfirmed,
                u.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountInvitationDto>> ListInvitationsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.AccountInvitations
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new AccountInvitationDto(
                i.Id,
                i.Email,
                i.Token,
                i.CreatedAt,
                i.ExpiresAt,
                i.AcceptedAt.HasValue,
                i.AcceptedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result> SetUserActiveStatusAsync(
        UserId userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            return Result.NotFound(Message.Error("accounts.admin.user_not_found"));
        }

        user.IsActive = isActive;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result.Invalid(Message.Error("accounts.admin.update_failed"));
        }

        if (!isActive)
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }

        return Result.Ok();
    }

    public async Task<Result<AccountInvitationDto>> CreateInvitationAsync(
        string email,
        UserId invitedBy,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = _userManager.NormalizeEmail(email) ?? email.ToUpperInvariant();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expirationHours = _registrationOptions.Value.InvitationExpirationHours;
        var expiresAt = now.AddHours(expirationHours);

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var invitation = new AccountInvitation
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            Token = rawToken,
            InvitedByUserId = invitedBy.Value,
            CreatedAt = now,
            ExpiresAt = expiresAt,
        };

        await _dbContext.AccountInvitations.AddAsync(invitation, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new AccountInvitationDto(
            invitation.Id,
            invitation.Email,
            invitation.Token,
            invitation.CreatedAt,
            invitation.ExpiresAt,
            IsAccepted: false,
            AcceptedAtUtc: null);

        return Result.Ok(dto);
    }

    public async Task<Result<AccountInvitationDto>> ValidateInvitationTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Invalid(Message.Error("accounts.invitation.invalid_token"));
        }

        var invitation = await _dbContext.AccountInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken);

        if (invitation is null)
        {
            return Result.Invalid(Message.Error("accounts.invitation.invalid_token"));
        }

        if (invitation.AcceptedAt.HasValue)
        {
            return Result.Invalid(Message.Error("accounts.invitation.already_accepted"));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (invitation.ExpiresAt < now)
        {
            return Result.Invalid(Message.Error("accounts.invitation.expired"));
        }

        var dto = new AccountInvitationDto(
            invitation.Id,
            invitation.Email,
            invitation.Token,
            invitation.CreatedAt,
            invitation.ExpiresAt,
            IsAccepted: false,
            AcceptedAtUtc: null);

        return Result.Ok(dto);
    }

    public async Task<Result> MarkInvitationAcceptedAsync(
        string token,
        UserId acceptedBy,
        CancellationToken cancellationToken = default)
    {
        var invitation = await _dbContext.AccountInvitations
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken);

        if (invitation is null)
        {
            return Result.NotFound(Message.Error("accounts.invitation.not_found"));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        invitation.AcceptedAt = now;
        invitation.AcceptedByUserId = acceptedBy.Value;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<AccountUserDto?> GetUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return null;
        }

        return new AccountUserDto(
            UserId.From(user.Id),
            user.Email ?? string.Empty,
            user.IsAdmin,
            user.IsActive,
            user.EmailConfirmed,
            user.CreatedAt);
    }

    public async Task<AccountUserDto?> GetUserByIdAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            return null;
        }

        return new AccountUserDto(
            UserId.From(user.Id),
            user.Email ?? string.Empty,
            user.IsAdmin,
            user.IsActive,
            user.EmailConfirmed,
            user.CreatedAt);
    }

    public Task<Result> ResetAdminPasswordAsync(
        string newPassword,
        CancellationToken cancellationToken = default) =>
        ResetAdminPasswordInternalAsync(email: null, newPassword, cancellationToken);

    public Task<Result> ResetAdminPasswordAsync(
        string email,
        string newPassword,
        CancellationToken cancellationToken = default) =>
        ResetAdminPasswordInternalAsync(email, newPassword, cancellationToken);

    private async Task<Result> ResetAdminPasswordInternalAsync(
        string? email,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return Result.Invalid(Message.Error("accounts.reset_password.password_required", nameof(newPassword)));
        }

        ApplicationUser? admin;
        if (string.IsNullOrWhiteSpace(email))
        {
            admin = await _userManager.Users
                .FirstOrDefaultAsync(u => u.IsAdmin, cancellationToken);

            if (admin is null)
            {
                return Result.NotFound(Message.Error("accounts.admin.not_found"));
            }
        }
        else
        {
            var normalizedEmail = _userManager.NormalizeEmail(email) ?? email.ToUpperInvariant();
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

            if (user is null)
            {
                return Result.NotFound(Message.Error("accounts.admin.not_found"));
            }

            if (!user.IsAdmin)
            {
                return Result.Invalid(Message.Error("accounts.admin.not_admin"));
            }

            admin = user;
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(admin);
        var resetResult = await _userManager.ResetPasswordAsync(admin, resetToken, newPassword);
        if (!resetResult.Succeeded)
        {
            return Result.Invalid(IdentityErrorMessages.ToMessages(
                resetResult.Errors,
                new IdentityErrorTargets(Password: nameof(newPassword))));
        }

        if (await _userManager.IsLockedOutAsync(admin))
        {
            await _userManager.SetLockoutEndDateAsync(admin, null);
        }
        await _userManager.ResetAccessFailedCountAsync(admin);

        admin.IsActive = true;
        admin.EmailConfirmed = true;
        var updateResult = await _userManager.UpdateAsync(admin);
        if (!updateResult.Succeeded)
        {
            return Result.Invalid(IdentityErrorMessages.ToMessages(updateResult.Errors, new IdentityErrorTargets()));
        }

        await _userManager.UpdateSecurityStampAsync(admin);
        return Result.Ok();
    }

    private static Failure EmailAlreadyExists() =>
        Result.Conflict(Message.Error("accounts.registration.email_already_exists"));
}
