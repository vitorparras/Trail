using System.Diagnostics.Metrics;
using Trail.Application.Accounts;
using Trail.Application.Telemetry;
using Trail.Domain.Common;

namespace Trail.Application.Tests.Accounts;

public sealed class RegisterAccountHandlerTests
{
    private readonly FakeAccountIdentityGateway _identityGateway = new();
    private readonly FakeUserSettingsStore _userSettingsStore = new();
    private readonly FakeEmailSender _emailSender = new();
    private readonly ApplicationMetrics _metrics = new(new RecordingMeterFactory());

    private RegisterAccountHandler CreateHandler() =>
        new(_identityGateway, _userSettingsStore, _emailSender, _metrics);

    [Fact]
    public async Task HandleAsync_FirstUserWithoutSmtp_CreatesAdminAutoConfirmedAndDefaultSettings()
    {
        _identityGateway.AnyUsersExistResult = false;
        _emailSender.IsSmtpConfigured = false;
        var handler = CreateHandler();

        var command = new RegisterAccountCommand("admin@example.com", "Password123!", "Password123!");
        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.False(result.Data.RequiresEmailConfirmation);
        Assert.NotNull(result.Data.UserId);

        var createdUser = Assert.Single(_identityGateway.Users);
        Assert.Equal("admin@example.com", createdUser.Email);
        Assert.True(createdUser.IsAdmin);
        Assert.True(createdUser.EmailConfirmed);

        Assert.NotNull(_userSettingsStore.Settings);
        Assert.Equal(createdUser.Id, _userSettingsStore.Settings.Id);
        Assert.True(_userSettingsStore.Saved);
        Assert.Empty(_emailSender.SentMessages);
    }

    [Fact]
    public async Task HandleAsync_FirstUserWithSmtp_CreatesAdminRequiresConfirmationAndSendsEmail()
    {
        _identityGateway.AnyUsersExistResult = false;
        _emailSender.IsSmtpConfigured = true;
        var handler = CreateHandler();

        var command = new RegisterAccountCommand(
            "admin@example.com",
            "Password123!",
            "Password123!",
            ConfirmationLinkBuilder: (userId, token) => $"https://trail.app/confirm?u={userId}&t={token}");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.True(result.Data.RequiresEmailConfirmation);

        var createdUser = Assert.Single(_identityGateway.Users);
        Assert.True(createdUser.IsAdmin);
        Assert.False(createdUser.EmailConfirmed);

        var sentMessage = Assert.Single(_emailSender.SentMessages);
        Assert.Equal("admin@example.com", sentMessage.ToAddress);
        Assert.Equal(EmailKinds.Confirmation, sentMessage.Kind);
        Assert.Contains("https://trail.app/confirm", sentMessage.HtmlBody);
    }

    [Fact]
    public async Task HandleAsync_FirstUserWithSmtpWhenEmailFails_StillReturnsRequiresConfirmation()
    {
        _identityGateway.AnyUsersExistResult = false;
        _emailSender.IsSmtpConfigured = true;
        _emailSender.SendResult = Result.Invalid(Message.Error("smtp.failed"));
        var handler = CreateHandler();

        var command = new RegisterAccountCommand("admin@example.com", "Password123!", "Password123!");
        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.True(result.Data.RequiresEmailConfirmation);
    }

    [Fact]
    public async Task HandleAsync_NonFirstUserClosedMode_ReturnsForbidden()
    {
        _identityGateway.AnyUsersExistResult = true;
        _identityGateway.RegistrationMode = RegistrationMode.Closed;
        var handler = CreateHandler();

        var command = new RegisterAccountCommand("user@example.com", "Password123!", "Password123!");
        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.registration.closed", message.Code);
    }

    [Fact]
    public async Task HandleAsync_NonFirstUserInviteOnlyWithoutToken_ReturnsForbiddenInviteRequired()
    {
        _identityGateway.AnyUsersExistResult = true;
        _identityGateway.RegistrationMode = RegistrationMode.InviteOnly;
        var handler = CreateHandler();

        var command = new RegisterAccountCommand("user@example.com", "Password123!", "Password123!", InviteToken: null);
        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.registration.invite_required", message.Code);
    }

    [Fact]
    public async Task HandleAsync_NonFirstUserInviteOnlyWithInvalidToken_ReturnsInvalidInvite()
    {
        _identityGateway.AnyUsersExistResult = true;
        _identityGateway.RegistrationMode = RegistrationMode.InviteOnly;
        var handler = CreateHandler();

        var command = new RegisterAccountCommand("user@example.com", "Password123!", "Password123!", InviteToken: "unknown-token");
        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.registration.invite_invalid", message.Code);
    }

    [Fact]
    public async Task HandleAsync_NonFirstUserInviteOnlyWithTokenForDifferentEmail_ReturnsInvalidInvite()
    {
        _identityGateway.AnyUsersExistResult = true;
        _identityGateway.RegistrationMode = RegistrationMode.InviteOnly;
        _identityGateway.Invitations.Add(new AccountInvitationDto(
            Guid.NewGuid(),
            "other@example.com",
            "valid-token",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            false,
            null));

        var handler = CreateHandler();
        var command = new RegisterAccountCommand("user@example.com", "Password123!", "Password123!", InviteToken: "valid-token");
        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.registration.invite_invalid", message.Code);
    }

    [Fact]
    public async Task HandleAsync_NonFirstUserInviteOnlyWithValidToken_CreatesUserAndMarksInviteAccepted()
    {
        _identityGateway.AnyUsersExistResult = true;
        _identityGateway.RegistrationMode = RegistrationMode.InviteOnly;
        _identityGateway.Invitations.Add(new AccountInvitationDto(
            Guid.NewGuid(),
            "user@example.com",
            "valid-token",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            false,
            null));

        var handler = CreateHandler();
        var command = new RegisterAccountCommand("user@example.com", "Password123!", "Password123!", InviteToken: "valid-token");
        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.True(result.Data.RequiresEmailConfirmation);

        var createdUser = Assert.Single(_identityGateway.Users);
        Assert.False(createdUser.IsAdmin);
        Assert.Equal("valid-token", _identityGateway.LastAcceptedToken);
        Assert.Equal(createdUser.Id, _identityGateway.LastAcceptedBy);
    }

    [Fact]
    public async Task HandleAsync_NonFirstUserOpenModeExistingEmail_ReturnsSuccessWithoutDuplicateUser()
    {
        _identityGateway.AnyUsersExistResult = true;
        _identityGateway.RegistrationMode = RegistrationMode.Open;
        var existingUserId = UserId.New();
        _identityGateway.Users.Add(new AccountUserDto(existingUserId, "existing@example.com", false, true, true, DateTime.UtcNow));

        var handler = CreateHandler();
        var command = new RegisterAccountCommand("existing@example.com", "Password123!", "Password123!");
        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.True(result.Data.RequiresEmailConfirmation);
        Assert.Single(_identityGateway.Users);
        Assert.Equal(["existing@example.com"], _identityGateway.SimulatedAccountCreations);
    }

    [Theory]
    [InlineData("existing@example.com")]
    [InlineData("new@example.com")]
    public async Task HandleAsync_NonFirstUserWithWeakPassword_ReturnsThePasswordRuleWhetherOrNotTheEmailExists(string email)
    {
        _identityGateway.AnyUsersExistResult = true;
        _identityGateway.RegistrationMode = RegistrationMode.Open;
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "existing@example.com", false, true, true, DateTime.UtcNow));
        _identityGateway.ValidatePasswordResult = Result.Invalid(Message.Error("accounts.identity.password_too_short", nameof(RegisterAccountCommand.Password)));

        var result = await CreateHandler().HandleAsync(new RegisterAccountCommand(email, "abcde", "abcde"), TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.identity.password_too_short", message.Code);
        Assert.Equal(nameof(RegisterAccountCommand.Password), message.Target);
        Assert.Single(_identityGateway.Users);
        Assert.Empty(_identityGateway.SimulatedAccountCreations);
    }

    [Fact]
    public async Task HandleAsync_NonFirstUserWhenCreationConflictsWithAConcurrentRegistration_ReturnsTheSameOkAsAnExistingEmail()
    {
        _identityGateway.AnyUsersExistResult = true;
        _identityGateway.RegistrationMode = RegistrationMode.Open;
        _identityGateway.CreateUserResult = Result.Conflict(Message.Error("accounts.registration.email_already_exists"));

        var result = await CreateHandler().HandleAsync(new RegisterAccountCommand("racing@example.com", "Password123!", "Password123!"), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(result.Messages);
        Assert.True(result.Data.RequiresEmailConfirmation);
        Assert.Null(result.Data.UserId);
        Assert.Null(_userSettingsStore.Settings);
        Assert.Empty(_emailSender.SentMessages);
    }

    [Fact]
    public async Task HandleAsync_NonFirstUserOpenModeNewEmail_CreatesStandardUserAndSendsConfirmation()
    {
        _identityGateway.AnyUsersExistResult = true;
        _identityGateway.RegistrationMode = RegistrationMode.Open;
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "admin@example.com", true, true, true, DateTime.UtcNow));

        var handler = CreateHandler();
        var command = new RegisterAccountCommand("new@example.com", "Password123!", "Password123!");
        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.True(result.Data.RequiresEmailConfirmation);

        Assert.Equal(2, _identityGateway.Users.Count);
        var newUser = _identityGateway.Users.First(u => u.Email == "new@example.com");
        Assert.False(newUser.IsAdmin);
        Assert.False(newUser.EmailConfirmed);

        var sentMessage = Assert.Single(_emailSender.SentMessages);
        Assert.Equal("new@example.com", sentMessage.ToAddress);
    }

    [Fact]
    public async Task HandleAsync_GatewayCreateUserFails_ReturnsFailure()
    {
        _identityGateway.AnyUsersExistResult = false;
        _identityGateway.CreateUserResult = Result.Invalid(Message.Error("identity.password_too_weak"));
        var handler = CreateHandler();

        var command = new RegisterAccountCommand("admin@example.com", "weak", "weak");
        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        var message = Assert.Single(result.Messages);
        Assert.Equal("identity.password_too_weak", message.Code);
    }

    [Theory]
    [InlineData(null, "Password123!", "Password123!", "accounts.registration.email_required")]
    [InlineData("", "Password123!", "Password123!", "accounts.registration.email_required")]
    [InlineData("invalid-email", "Password123!", "Password123!", "accounts.registration.email_invalid")]
    [InlineData("valid@example.com", null, "Password123!", "accounts.registration.password_required")]
    [InlineData("valid@example.com", "", "", "accounts.registration.password_required")]
    [InlineData("valid@example.com", "Password123!", "Mismatch123!", "accounts.registration.passwords_do_not_match")]
    public async Task HandleAsync_ValidationErrors_ReturnsInvalidWithExpectedCode(
        string? email,
        string? password,
        string? confirmPassword,
        string expectedCode)
    {
        var handler = CreateHandler();
        var command = new RegisterAccountCommand(email!, password!, confirmPassword!);
        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains(result.Messages, m => m.Code == expectedCode);
    }

    private sealed class RecordingMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var meter in _meters)
            {
                meter.Dispose();
            }
        }
    }
}
