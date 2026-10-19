using System.Data.Common;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Trail.Application.Abstractions;
using Trail.Application.Accounts;
using Trail.Application.Tests.Persistence;
using Trail.Domain.Accounts;
using Trail.Domain.Common;
using Trail.Infrastructure.Email;
using Trail.Infrastructure.Identity;
using Trail.Infrastructure.Persistence;
using Trail.Web.Cli;

namespace Trail.Application.Tests.Accounts;

public sealed class IdentityAndEmailPersistenceTests
{
    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task AccountIdentityGateway_And_UserSettings_DualDatabase_FullLifecycle(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = await CreateHarnessAsync(provider, RegistrationMode.InviteOnly, ct);
        await using (harness.Cleanup)
        {
            var initialUsersExist = await harness.Gateway.AnyUsersExistAsync(ct);
            Assert.False(initialUsersExist);

            var createAdminResult = await harness.Gateway.CreateUserAsync(
                "admin@trail.local",
                "AdminPassword123!",
                isAdmin: true,
                emailConfirmed: true,
                ct);

            Assert.True(createAdminResult.IsOk);
            var adminId = createAdminResult.Data;

            var usersExistNow = await harness.Gateway.AnyUsersExistAsync(ct);
            Assert.True(usersExistNow);

            var duplicateAdminResult = await harness.Gateway.CreateUserAsync(
                "admin@trail.local",
                "AnotherPassword123!",
                isAdmin: false,
                emailConfirmed: true,
                ct);

            Assert.False(duplicateAdminResult.IsOk);
            Assert.Equal(ResultStatus.Conflict, duplicateAdminResult.Status);

            var adminByEmail = await harness.Gateway.GetUserByEmailAsync("admin@trail.local", ct);
            Assert.NotNull(adminByEmail);
            Assert.True(adminByEmail.IsAdmin);
            Assert.True(adminByEmail.IsActive);
            Assert.True(adminByEmail.EmailConfirmed);

            var adminById = await harness.Gateway.GetUserByIdAsync(adminId, ct);
            Assert.NotNull(adminById);
            Assert.Equal(adminId, adminById.Id);

            harness.CurrentUser.Id = adminId;
            harness.CurrentUser.IsAuthenticated = true;
            harness.CurrentUser.IsAdmin = true;

            var initialSettings = await harness.SettingsStore.GetForCurrentUserAsync(ct);
            Assert.Null(initialSettings);

            var defaultSettings = UserSettings.CreateDefault(adminId);
            await harness.SettingsStore.AddAsync(defaultSettings, ct);
            await harness.SettingsStore.SaveChangesAsync(ct);

            var loadedSettings = await harness.SettingsStore.GetForCurrentUserAsync(ct);
            Assert.NotNull(loadedSettings);
            Assert.Equal(UserSettings.DefaultTimeZoneId, loadedSettings.TimeZoneId);
            Assert.Equal(UserSettings.DefaultPomodoroFocusMinutes, loadedSettings.PomodoroFocusMinutes);

            var updateResult = loadedSettings.Update("America/New_York", 50, 10);
            Assert.True(updateResult.IsOk);
            await harness.SettingsStore.SaveChangesAsync(ct);

            var reloadedSettings = await harness.SettingsStore.GetForCurrentUserAsync(ct);
            Assert.NotNull(reloadedSettings);
            Assert.Equal("America/New_York", reloadedSettings.TimeZoneId);
            Assert.Equal(50, reloadedSettings.PomodoroFocusMinutes);
            Assert.Equal(10, reloadedSettings.PomodoroBreakMinutes);

            var signInAdminSuccess = await harness.Gateway.SignInWithPasswordAsync("admin@trail.local", "AdminPassword123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.Success, signInAdminSuccess);

            var signInWrongPassword = await harness.Gateway.SignInWithPasswordAsync("admin@trail.local", "WrongPassword123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.InvalidCredentials, signInWrongPassword);

            var signInNotFound = await harness.Gateway.SignInWithPasswordAsync("nonexistent@trail.local", "Password123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.InvalidCredentials, signInNotFound);

            var createRegularResult = await harness.Gateway.CreateUserAsync(
                "user@trail.local",
                "UserPassword123!",
                isAdmin: false,
                emailConfirmed: false,
                ct);

            Assert.True(createRegularResult.IsOk);
            var userId = createRegularResult.Data;

            var signInUnconfirmed = await harness.Gateway.SignInWithPasswordAsync("user@trail.local", "UserPassword123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.EmailNotConfirmed, signInUnconfirmed);

            var confirmTokenResult = await harness.Gateway.GenerateEmailConfirmationTokenAsync(userId, ct);
            Assert.True(confirmTokenResult.IsOk);

            var invalidConfirmResult = await harness.Gateway.ConfirmEmailAsync(userId, "invalid-token", ct);
            Assert.False(invalidConfirmResult.IsOk);

            var validConfirmResult = await harness.Gateway.ConfirmEmailAsync(userId, confirmTokenResult.Data, ct);
            Assert.True(validConfirmResult.IsOk);

            var signInAfterConfirm = await harness.Gateway.SignInWithPasswordAsync("user@trail.local", "UserPassword123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.Success, signInAfterConfirm);

            var deactivateResult = await harness.Gateway.SetUserActiveStatusAsync(userId, isActive: false, ct);
            Assert.True(deactivateResult.IsOk);

            var signInDeactivated = await harness.Gateway.SignInWithPasswordAsync("user@trail.local", "UserPassword123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.Deactivated, signInDeactivated);

            var activateResult = await harness.Gateway.SetUserActiveStatusAsync(userId, isActive: true, ct);
            Assert.True(activateResult.IsOk);

            var signInReactivated = await harness.Gateway.SignInWithPasswordAsync("user@trail.local", "UserPassword123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.Success, signInReactivated);

            var changePasswordResult = await harness.Gateway.ChangePasswordAsync(userId, "UserPassword123!", "NewUserPassword123!", ct);
            Assert.True(changePasswordResult.IsOk);

            var signInOldPassword = await harness.Gateway.SignInWithPasswordAsync("user@trail.local", "UserPassword123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.InvalidCredentials, signInOldPassword);

            var signInNewPassword = await harness.Gateway.SignInWithPasswordAsync("user@trail.local", "NewUserPassword123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.Success, signInNewPassword);

            var resetTokenResult = await harness.Gateway.GeneratePasswordResetTokenAsync(userId, ct);
            Assert.True(resetTokenResult.IsOk);

            var resetPasswordResult = await harness.Gateway.ResetPasswordAsync(userId, resetTokenResult.Data, "ResetPassword123!", ct);
            Assert.True(resetPasswordResult.IsOk);

            var signInResetPassword = await harness.Gateway.SignInWithPasswordAsync("user@trail.local", "ResetPassword123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.Success, signInResetPassword);

            for (var i = 0; i < 5; i++)
            {
                await harness.Gateway.SignInWithPasswordAsync("user@trail.local", "BadPasswordAttempt123!", isPersistent: false, ct);
            }

            var signInLockedOut = await harness.Gateway.SignInWithPasswordAsync("user@trail.local", "BadPasswordAttempt123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.LockedOut, signInLockedOut);

            var inviteResult = await harness.Gateway.CreateInvitationAsync("invited@trail.local", adminId, ct);
            Assert.True(inviteResult.IsOk);
            var invitation = inviteResult.Data;
            Assert.Equal("invited@trail.local", invitation.Email);
            Assert.False(invitation.IsAccepted);

            var allInvitations = await harness.Gateway.ListInvitationsAsync(ct);
            Assert.Contains(allInvitations, i => i.Id == invitation.Id);

            var validateSuccess = await harness.Gateway.ValidateInvitationTokenAsync(invitation.Token, ct);
            Assert.True(validateSuccess.IsOk);

            var validateInvalid = await harness.Gateway.ValidateInvitationTokenAsync("bogus-token", ct);
            Assert.False(validateInvalid.IsOk);

            var markAcceptedResult = await harness.Gateway.MarkInvitationAcceptedAsync(invitation.Token, adminId, ct);
            Assert.True(markAcceptedResult.IsOk);

            var validateAlreadyAccepted = await harness.Gateway.ValidateInvitationTokenAsync(invitation.Token, ct);
            Assert.False(validateAlreadyAccepted.IsOk);

            var allUsers = await harness.Gateway.ListUsersAsync(ct);
            Assert.Equal(2, allUsers.Count);
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task ResetPasswordAsync_WithPasswordBelowMinimumLength_ReturnsThePasswordRuleOnNewPasswordAndKeepsTheToken(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = await CreateHarnessAsync(provider, cancellationToken: ct);
        await using (harness.Cleanup)
        {
            var userId = (await harness.Gateway.CreateUserAsync("reset@trail.local", "InitialPass123!", isAdmin: false, emailConfirmed: true, ct)).Data;
            var token = (await harness.Gateway.GeneratePasswordResetTokenAsync(userId, ct)).Data;

            var shortPasswordResult = await harness.Gateway.ResetPasswordAsync(userId, token, "abc", ct);

            Assert.Equal(ResultStatus.Invalid, shortPasswordResult.Status);
            var message = Assert.Single(shortPasswordResult.Messages);
            Assert.Equal("accounts.identity.password_too_short", message.Code);
            Assert.Equal(nameof(ResetPasswordCommand.NewPassword), message.Target);

            var validPasswordResult = await harness.Gateway.ResetPasswordAsync(userId, token, "ResetPass456!", ct);
            Assert.True(validPasswordResult.IsOk);
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task ResetPasswordAsync_WithInvalidTokenOrUnknownUser_ReturnsTheSameInvalidLink(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = await CreateHarnessAsync(provider, cancellationToken: ct);
        await using (harness.Cleanup)
        {
            var userId = (await harness.Gateway.CreateUserAsync("reset@trail.local", "InitialPass123!", isAdmin: false, emailConfirmed: true, ct)).Data;

            Result[] results =
            [
                await harness.Gateway.ResetPasswordAsync(userId, "invalid-token", "ResetPass456!", ct),
                await harness.Gateway.ResetPasswordAsync(userId, "invalid-token", "abc", ct),
                await harness.Gateway.ResetPasswordAsync(UserId.New(), "invalid-token", "ResetPass456!", ct),
            ];

            Assert.All(results, result =>
            {
                Assert.Equal(ResultStatus.Invalid, result.Status);
                var message = Assert.Single(result.Messages);
                Assert.Equal("accounts.reset_password.invalid_token", message.Code);
                Assert.Null(message.Target);
            });
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task ConfirmEmailAsync_WithInvalidTokenOrUnknownUser_ReturnsTheSameInvalidLink(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = await CreateHarnessAsync(provider, cancellationToken: ct);
        await using (harness.Cleanup)
        {
            var userId = (await harness.Gateway.CreateUserAsync("confirm@trail.local", "InitialPass123!", isAdmin: false, emailConfirmed: false, ct)).Data;

            Result[] results =
            [
                await harness.Gateway.ConfirmEmailAsync(userId, "invalid-token", ct),
                await harness.Gateway.ConfirmEmailAsync(UserId.New(), "invalid-token", ct),
            ];

            Assert.All(results, result =>
            {
                Assert.Equal(ResultStatus.Invalid, result.Status);
                Assert.Equal("accounts.confirmation.invalid_token", Assert.Single(result.Messages).Code);
            });
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task SignInWithPasswordAsync_WrongPasswordsOnUnconfirmedOrDeactivatedAccounts_AnswerAsUnknownAndCountTowardTheLockout(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = await CreateHarnessAsync(provider, cancellationToken: ct);
        await using (harness.Cleanup)
        {
            const string password = "RightPass123!";
            const string wrongPassword = "WrongPass123!";
            await harness.Gateway.CreateUserAsync("unconfirmed@trail.local", password, isAdmin: false, emailConfirmed: false, ct);
            var deactivatedId = (await harness.Gateway.CreateUserAsync("deactivated@trail.local", password, isAdmin: false, emailConfirmed: true, ct)).Data;
            await harness.Gateway.SetUserActiveStatusAsync(deactivatedId, isActive: false, ct);

            foreach (var email in new[] { "unconfirmed@trail.local", "deactivated@trail.local" })
            {
                for (var attempt = 1; attempt < 5; attempt++)
                {
                    Assert.Equal(PasswordSignInResult.InvalidCredentials, await harness.Gateway.SignInWithPasswordAsync(email, wrongPassword, isPersistent: false, ct));
                }

                Assert.Equal(PasswordSignInResult.LockedOut, await harness.Gateway.SignInWithPasswordAsync(email, wrongPassword, isPersistent: false, ct));
                Assert.Equal(PasswordSignInResult.LockedOut, await harness.Gateway.SignInWithPasswordAsync(email, password, isPersistent: false, ct));
            }

            Assert.Equal(PasswordSignInResult.InvalidCredentials, await harness.Gateway.SignInWithPasswordAsync("unknown@trail.local", wrongPassword, isPersistent: false, ct));
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task SignInWithPasswordAsync_CorrectPasswordOnAnUnconfirmedAccount_ReportsTheStateAndResetsTheFailedAttempts(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = await CreateHarnessAsync(provider, cancellationToken: ct);
        await using (harness.Cleanup)
        {
            const string email = "unconfirmed@trail.local";
            const string password = "RightPass123!";
            await harness.Gateway.CreateUserAsync(email, password, isAdmin: false, emailConfirmed: false, ct);

            for (var attempt = 1; attempt < 5; attempt++)
            {
                await harness.Gateway.SignInWithPasswordAsync(email, "WrongPass123!", isPersistent: false, ct);
            }

            Assert.Equal(PasswordSignInResult.EmailNotConfirmed, await harness.Gateway.SignInWithPasswordAsync(email, password, isPersistent: false, ct));
            Assert.Equal(PasswordSignInResult.InvalidCredentials, await harness.Gateway.SignInWithPasswordAsync(email, "WrongPass123!", isPersistent: false, ct));
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task ValidatePasswordAsync_AppliesTheConfiguredPasswordRulesWithoutCreatingAUser(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = await CreateHarnessAsync(provider, cancellationToken: ct);
        await using (harness.Cleanup)
        {
            var weak = await harness.Gateway.ValidatePasswordAsync("weak@trail.local", "abcde", ct);
            var strong = await harness.Gateway.ValidatePasswordAsync("strong@trail.local", "StrongPass123!", ct);
            await harness.Gateway.SimulateAccountCreationAsync("simulated@trail.local", "StrongPass123!", ct);

            Assert.Equal(ResultStatus.Invalid, weak.Status);
            var message = Assert.Single(weak.Messages);
            Assert.Equal("accounts.identity.password_too_short", message.Code);
            Assert.Equal(nameof(RegisterAccountCommand.Password), message.Target);
            Assert.True(strong.IsOk);
            Assert.False(await harness.Gateway.AnyUsersExistAsync(ct));
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task ChangePasswordAsync_WithIdentityFailures_ReturnsTranslatableCodesOnTheirFields(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = await CreateHarnessAsync(provider, cancellationToken: ct);
        await using (harness.Cleanup)
        {
            var userId = (await harness.Gateway.CreateUserAsync("change@trail.local", "InitialPass123!", isAdmin: false, emailConfirmed: true, ct)).Data;

            var wrongCurrentResult = await harness.Gateway.ChangePasswordAsync(userId, "WrongPass123!", "ChangedPass456!", ct);
            var shortNewResult = await harness.Gateway.ChangePasswordAsync(userId, "InitialPass123!", "abc", ct);

            var mismatch = Assert.Single(wrongCurrentResult.Messages);
            Assert.Equal("accounts.identity.password_mismatch", mismatch.Code);
            Assert.Equal(nameof(ChangePasswordCommand.CurrentPassword), mismatch.Target);
            var tooShort = Assert.Single(shortNewResult.Messages);
            Assert.Equal("accounts.identity.password_too_short", tooShort.Code);
            Assert.Equal(nameof(ChangePasswordCommand.NewPassword), tooShort.Target);
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task CreateUserAsync_WithIdentityFailures_ReturnsTranslatableCodesOnTheirFields(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = await CreateHarnessAsync(provider, cancellationToken: ct);
        await using (harness.Cleanup)
        {
            var shortPasswordResult = await harness.Gateway.CreateUserAsync("short@trail.local", "abc", isAdmin: false, emailConfirmed: true, ct);
            var invalidUserNameResult = await harness.Gateway.CreateUserAsync("josé@trail.local", "ValidPass123!", isAdmin: false, emailConfirmed: true, ct);

            Assert.Equal(ResultStatus.Invalid, shortPasswordResult.Status);
            var tooShort = Assert.Single(shortPasswordResult.Messages);
            Assert.Equal("accounts.identity.password_too_short", tooShort.Code);
            Assert.Equal(nameof(RegisterAccountCommand.Password), tooShort.Target);
            Assert.Equal(ResultStatus.Invalid, invalidUserNameResult.Status);
            var invalidUserName = Assert.Single(invalidUserNameResult.Messages);
            Assert.Equal("accounts.identity.invalid_user_name", invalidUserName.Code);
            Assert.Equal(nameof(RegisterAccountCommand.Email), invalidUserName.Target);
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task ResetAdminPasswordCommand_ResetsPasswordAndUnlocksLockedAccount(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = await CreateHarnessAsync(provider, cancellationToken: ct);
        await using (harness.Cleanup)
        {
            await harness.Gateway.CreateUserAsync("admin@trail.local", "InitialAdminPass123!", isAdmin: true, emailConfirmed: true, ct);

            for (var i = 0; i < 5; i++)
            {
                await harness.Gateway.SignInWithPasswordAsync("admin@trail.local", "WrongPass123!", isPersistent: false, ct);
            }

            var lockCheck = await harness.Gateway.SignInWithPasswordAsync("admin@trail.local", "WrongPass123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.LockedOut, lockCheck);

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var exitCode = await ResetAdminPasswordCommand.RunAsync(
                ["--reset-admin-password", "AdminNewSecret123!"],
                harness.Gateway,
                stdout,
                stderr,
                ct);

            Assert.Equal(0, exitCode);
            Assert.Contains("successfully reset", stdout.ToString(), StringComparison.Ordinal);

            var signInResult = await harness.Gateway.SignInWithPasswordAsync("admin@trail.local", "AdminNewSecret123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.Success, signInResult);
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task ResetAdminPasswordCommand_WithEmailAndNewPassword_ResetsPasswordAndUnlocksAccount(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = await CreateHarnessAsync(provider, cancellationToken: ct);
        await using (harness.Cleanup)
        {
            await harness.Gateway.CreateUserAsync("firstadmin@trail.local", "FirstPass123!", isAdmin: true, emailConfirmed: true, ct);
            await harness.Gateway.CreateUserAsync("secondadmin@trail.local", "SecondPass123!", isAdmin: true, emailConfirmed: true, ct);

            for (var i = 0; i < 5; i++)
            {
                await harness.Gateway.SignInWithPasswordAsync("secondadmin@trail.local", "WrongPass123!", isPersistent: false, ct);
            }

            var lockCheck = await harness.Gateway.SignInWithPasswordAsync("secondadmin@trail.local", "WrongPass123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.LockedOut, lockCheck);

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var exitCode = await ResetAdminPasswordCommand.RunAsync(
                ["--reset-admin-password", "secondadmin@trail.local", "SecondNewSecret123!"],
                harness.Gateway,
                stdout,
                stderr,
                ct);

            Assert.Equal(0, exitCode);
            Assert.Contains("successfully reset", stdout.ToString(), StringComparison.Ordinal);

            var signInResult = await harness.Gateway.SignInWithPasswordAsync("secondadmin@trail.local", "SecondNewSecret123!", isPersistent: false, ct);
            Assert.Equal(PasswordSignInResult.Success, signInResult);
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task ResetAdminPasswordCommand_WithNonAdminEmail_ReturnsExitCode1(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = await CreateHarnessAsync(provider, cancellationToken: ct);
        await using (harness.Cleanup)
        {
            await harness.Gateway.CreateUserAsync("regular@trail.local", "RegularPass123!", isAdmin: false, emailConfirmed: true, ct);

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var exitCode = await ResetAdminPasswordCommand.RunAsync(
                ["--reset-admin-password", "regular@trail.local", "NewPassword123!"],
                harness.Gateway,
                stdout,
                stderr,
                ct);

            Assert.Equal(1, exitCode);
            Assert.Contains("Error", stderr.ToString(), StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task ResetAdminPasswordCommand_WithNonexistentEmail_ReturnsExitCode1(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = await CreateHarnessAsync(provider, cancellationToken: ct);
        await using (harness.Cleanup)
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var exitCode = await ResetAdminPasswordCommand.RunAsync(
                ["--reset-admin-password", "nonexistent@trail.local", "NewPassword123!"],
                harness.Gateway,
                stdout,
                stderr,
                ct);

            Assert.Equal(1, exitCode);
            Assert.Contains("Error", stderr.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ResetAdminPasswordCommand_MissingPasswordArgument_ReturnsExitCode1()
    {
        var ct = TestContext.Current.CancellationToken;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await ResetAdminPasswordCommand.RunAsync(
            ["--reset-admin-password"],
            new FakeAccountIdentityGateway(),
            stdout,
            stderr,
            ct);

        Assert.Equal(1, exitCode);
        Assert.Contains("Error", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetAdminPasswordCommand_BlankPasswordArgument_ReturnsExitCode1()
    {
        var ct = TestContext.Current.CancellationToken;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await ResetAdminPasswordCommand.RunAsync(
            ["--reset-admin-password", "admin@trail.local", "   "],
            new FakeAccountIdentityGateway(),
            stdout,
            stderr,
            ct);

        Assert.Equal(1, exitCode);
        Assert.Contains("Error", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailKitEmailSender_RealMailpit_SendsEmailAndVerifiesInApi()
    {
        var ct = TestContext.Current.CancellationToken;
        var mailpit = new ContainerBuilder("axllent/mailpit:v1.24.0")
            .WithPortBinding(1025, true)
            .WithPortBinding(8025, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/api/v1/info")))
            .Build();

        await mailpit.StartAsync(ct);
        try
        {
            var smtpPort = mailpit.GetMappedPublicPort(1025);
            var httpPort = mailpit.GetMappedPublicPort(8025);
            var host = mailpit.Hostname;

            var options = Options.Create(new EmailOptions
            {
                Host = host,
                Port = smtpPort,
                FromAddress = "noreply@trail.local",
                FromName = "Trail",
            });

            using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            var sender = new MailKitEmailSender(options, loggerFactory.CreateLogger<MailKitEmailSender>());

            Assert.True(sender.IsSmtpConfigured);

            var message = new EmailMessage(
                "student@trail.local",
                "Trail Confirmation",
                "<p>Welcome to Trail!</p>",
                "Welcome to Trail!",
                EmailKinds.Confirmation);

            var sendResult = await sender.SendAsync(message, ct);
            Assert.True(sendResult.IsOk);

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"http://{host}:{httpPort}/api/v1/messages", ct);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct);
            Assert.Contains("Trail Confirmation", content, StringComparison.Ordinal);
            Assert.Contains("student@trail.local", content, StringComparison.Ordinal);
        }
        finally
        {
            await mailpit.DisposeAsync();
        }
    }

    [Fact]
    public async Task MailKitEmailSender_UnreachableSmtp_ReturnsInvalidWithoutThrowing()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = Options.Create(new EmailOptions
        {
            Host = "127.0.0.1",
            Port = 54321,
            FromAddress = "noreply@trail.local",
            FromName = "Trail",
        });

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var sender = new MailKitEmailSender(options, loggerFactory.CreateLogger<MailKitEmailSender>());

        var message = new EmailMessage(
            "student@trail.local",
            "Failing Email",
            "<p>Will Fail</p>",
            "Will Fail",
            EmailKinds.Test);

        var result = await sender.SendAsync(message, ct);
        Assert.False(result.IsOk);
        Assert.Contains(result.Messages, m => m.Code == "accounts.email.send_failed");
    }

    [Fact]
    public async Task MailKitEmailSender_UnconfiguredSmtp_ReturnsInvalid()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = Options.Create(new EmailOptions
        {
            Host = null,
        });

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var sender = new MailKitEmailSender(options, loggerFactory.CreateLogger<MailKitEmailSender>());

        Assert.False(sender.IsSmtpConfigured);

        var message = new EmailMessage(
            "student@trail.local",
            "Unconfigured Email",
            "<p>Unconfigured</p>",
            "Unconfigured",
            EmailKinds.Test);

        var result = await sender.SendAsync(message, ct);
        Assert.False(result.IsOk);
        Assert.Contains(result.Messages, m => m.Code == "accounts.email.smtp_not_configured");
    }

    private static async Task<(ServiceProvider Services, AppDbContext DbContext, IAccountIdentityGateway Gateway, IUserSettingsStore SettingsStore, TestCurrentUser CurrentUser, IAsyncDisposable Cleanup)> CreateHarnessAsync(
        DatabaseProvider provider,
        RegistrationMode registrationMode = RegistrationMode.Open,
        CancellationToken cancellationToken = default)
    {
        DbConnection? sqliteConnection = null;
        string? postgresSchema = null;
        string? postgresConnectionString = null;

        var services = new ServiceCollection();
        services.AddLogging();

        var currentUser = new TestCurrentUser { IsAuthenticated = false };
        services.AddSingleton<ICurrentUser>(currentUser);
        services.AddSingleton(TimeProvider.System);

        services.Configure<RegistrationOptions>(opt =>
        {
            opt.Mode = registrationMode;
            opt.InvitationExpirationHours = 48;
        });

        if (provider == DatabaseProvider.Sqlite)
        {
            sqliteConnection = new SqliteConnection("Data Source=:memory:");
            await sqliteConnection.OpenAsync(cancellationToken);
            services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(sqliteConnection));
        }
        else
        {
            var baseConnectionString = await PostgreSqlTestDatabase.GetConnectionStringAsync(cancellationToken);
            postgresSchema = $"test_id_{Guid.NewGuid():N}";
            await using (var connection = new NpgsqlConnection(baseConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE SCHEMA \"{postgresSchema}\";";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var csb = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                SearchPath = postgresSchema,
            };
            postgresConnectionString = baseConnectionString;
            services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(csb.ConnectionString));
        }

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme);

        services.AddIdentityCore<ApplicationUser>(opt =>
        {
            opt.Lockout.MaxFailedAccessAttempts = 5;
            opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            opt.Lockout.AllowedForNewUsers = true;
            opt.User.RequireUniqueEmail = true;
            opt.Password.RequiredLength = 6;
            opt.Password.RequireDigit = false;
            opt.Password.RequireLowercase = false;
            opt.Password.RequireNonAlphanumeric = false;
            opt.Password.RequireUppercase = false;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddSignInManager<SignInManager<ApplicationUser>>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(opt =>
        {
            opt.TokenLifespan = TimeSpan.FromHours(2);
        });

        services.AddHttpContextAccessor();
        services.AddScoped<IAccountIdentityGateway, AccountIdentityGateway>();
        services.AddScoped<IUserSettingsStore, EfUserSettingsStore>();

        var serviceProvider = services.BuildServiceProvider();
        var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
        if (provider == DatabaseProvider.Sqlite)
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            var script = dbContext.Database.GenerateCreateScript();
            await dbContext.Database.ExecuteSqlRawAsync(script, cancellationToken);
        }

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
        };
        serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        var gateway = serviceProvider.GetRequiredService<IAccountIdentityGateway>();
        var settingsStore = serviceProvider.GetRequiredService<IUserSettingsStore>();

        var cleanup = new HarnessCleanup(sqliteConnection, postgresSchema, postgresConnectionString, serviceProvider);

        return (serviceProvider, dbContext, gateway, settingsStore, currentUser, cleanup);
    }

    private sealed class HarnessCleanup : IAsyncDisposable
    {
        private readonly DbConnection? _sqliteConnection;
        private readonly string? _postgresSchema;
        private readonly string? _postgresConnectionString;
        private readonly ServiceProvider _serviceProvider;

        public HarnessCleanup(
            DbConnection? sqliteConnection,
            string? postgresSchema,
            string? postgresConnectionString,
            ServiceProvider serviceProvider)
        {
            _sqliteConnection = sqliteConnection;
            _postgresSchema = postgresSchema;
            _postgresConnectionString = postgresConnectionString;
            _serviceProvider = serviceProvider;
        }

        public async ValueTask DisposeAsync()
        {
            await _serviceProvider.DisposeAsync();

            if (_sqliteConnection is not null)
            {
                await _sqliteConnection.DisposeAsync();
            }

            if (_postgresSchema is not null && _postgresConnectionString is not null)
            {
                await using var connection = new NpgsqlConnection(_postgresConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP SCHEMA IF EXISTS \"{_postgresSchema}\" CASCADE;";
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
