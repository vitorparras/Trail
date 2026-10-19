using System.Diagnostics.Metrics;
using Trail.Application.Accounts;
using Trail.Application.Telemetry;
using Trail.Domain.Common;

namespace Trail.Application.Tests.Accounts;

public sealed class AdminHandlersTests
{
    private readonly TestCurrentUser _currentUser = new() { IsAdmin = true };
    private readonly FakeAccountIdentityGateway _identityGateway = new();
    private readonly FakeEmailSender _emailSender = new();
    private readonly FallbackAccountEmailContent _emailContent = new();
    private readonly ApplicationMetrics _metrics = new(new RecordingMeterFactory());

    [Fact]
    public async Task ListUsers_NonAdmin_ReturnsForbidden()
    {
        _currentUser.IsAdmin = false;
        var handler = new ListUsersHandler(_currentUser, _identityGateway);

        var result = await handler.HandleAsync(new ListUsersQuery(), TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.admin.forbidden", message.Code);
    }

    [Fact]
    public async Task ListUsers_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new ListUsersHandler(_currentUser, _identityGateway);

        var result = await handler.HandleAsync(new ListUsersQuery(), TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task ListUsers_Admin_ReturnsOverviewWithUsersAndInvitations()
    {
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "u1@example.com", false, true, true, DateTime.UtcNow));
        _identityGateway.Invitations.Add(new AccountInvitationDto(Guid.NewGuid(), "i1@example.com", "token", DateTime.UtcNow, DateTime.UtcNow.AddDays(7), false, null));
        var handler = new ListUsersHandler(_currentUser, _identityGateway);

        var result = await handler.HandleAsync(new ListUsersQuery(), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Single(result.Data.Users);
        Assert.Single(result.Data.Invitations);
    }

    [Theory]
    [InlineData(RegistrationMode.Closed)]
    [InlineData(RegistrationMode.InviteOnly)]
    [InlineData(RegistrationMode.Open)]
    public async Task ListUsers_Admin_ReportsTheRegistrationMode(RegistrationMode mode)
    {
        _identityGateway.RegistrationMode = mode;
        var handler = new ListUsersHandler(_currentUser, _identityGateway);

        var result = await handler.HandleAsync(new ListUsersQuery(), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal(mode, result.Data.RegistrationMode);
    }

    [Fact]
    public async Task SetUserActiveStatus_NonAdmin_ReturnsForbidden()
    {
        _currentUser.IsAdmin = false;
        var handler = new SetUserActiveStatusHandler(_currentUser, _identityGateway);

        var result = await handler.HandleAsync(new SetUserActiveStatusCommand(UserId.New(), false), TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task SetUserActiveStatus_AdminDeactivatingSelf_ReturnsInvalid()
    {
        var handler = new SetUserActiveStatusHandler(_currentUser, _identityGateway);

        var result = await handler.HandleAsync(new SetUserActiveStatusCommand(_currentUser.Id, false), TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.admin.cannot_deactivate_self", message.Code);
    }

    [Fact]
    public async Task SetUserActiveStatus_AdminActivatingSelf_CallsGatewayAndReturnsOk()
    {
        var handler = new SetUserActiveStatusHandler(_currentUser, _identityGateway);

        var result = await handler.HandleAsync(new SetUserActiveStatusCommand(_currentUser.Id, true), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
    }

    [Fact]
    public async Task SetUserActiveStatus_AdminDeactivatingOtherUser_CallsGatewayAndReturnsOk()
    {
        var handler = new SetUserActiveStatusHandler(_currentUser, _identityGateway);
        var otherUserId = UserId.New();

        var result = await handler.HandleAsync(new SetUserActiveStatusCommand(otherUserId, false), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
    }

    [Fact]
    public async Task CreateInvitation_NonAdmin_ReturnsForbidden()
    {
        _currentUser.IsAdmin = false;
        var handler = new CreateInvitationHandler(_currentUser, _identityGateway, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(new CreateInvitationCommand("invitee@example.com"), TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task CreateInvitation_RegistrationClosed_ReturnsForbiddenWithItsOwnMessageAndCreatesNothing()
    {
        _identityGateway.RegistrationMode = RegistrationMode.Closed;
        _emailSender.IsSmtpConfigured = true;
        var handler = new CreateInvitationHandler(_currentUser, _identityGateway, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(new CreateInvitationCommand("invitee@example.com"), TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.invitation.registration_closed", message.Code);
        Assert.Empty(_identityGateway.Invitations);
        Assert.Empty(_emailSender.SentMessages);
    }

    [Fact]
    public async Task CreateInvitation_RegistrationClosedAndNonAdmin_ReturnsTheAdministratorRefusal()
    {
        _identityGateway.RegistrationMode = RegistrationMode.Closed;
        _currentUser.IsAdmin = false;
        var handler = new CreateInvitationHandler(_currentUser, _identityGateway, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(new CreateInvitationCommand("invitee@example.com"), TestContext.Current.CancellationToken);

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.admin.forbidden", message.Code);
    }

    [Theory]
    [InlineData(RegistrationMode.InviteOnly)]
    [InlineData(RegistrationMode.Open)]
    public async Task CreateInvitation_RegistrationAcceptsInvitations_CreatesTheInvitation(RegistrationMode mode)
    {
        _identityGateway.RegistrationMode = mode;
        var handler = new CreateInvitationHandler(_currentUser, _identityGateway, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(new CreateInvitationCommand("invitee@example.com"), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Single(_identityGateway.Invitations);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateInvitation_MissingEmail_ReturnsInvalid(string? email)
    {
        var handler = new CreateInvitationHandler(_currentUser, _identityGateway, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(new CreateInvitationCommand(email!), TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.invitation.email_required", message.Code);
    }

    [Fact]
    public async Task CreateInvitation_UserAlreadyExists_ReturnsConflict()
    {
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "existing@example.com", false, true, true, DateTime.UtcNow));
        var handler = new CreateInvitationHandler(_currentUser, _identityGateway, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(new CreateInvitationCommand("existing@example.com"), TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.invitation.user_already_exists", message.Code);
    }

    [Fact]
    public async Task CreateInvitation_GatewayFails_ReturnsFailure()
    {
        _identityGateway.CreateInvitationResult = Result.Invalid(Message.Error("invitation.failed"));
        var handler = new CreateInvitationHandler(_currentUser, _identityGateway, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(new CreateInvitationCommand("new@example.com"), TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        var message = Assert.Single(result.Messages);
        Assert.Equal("invitation.failed", message.Code);
    }

    [Fact]
    public async Task CreateInvitation_SmtpConfiguredSendSucceeds_SendsEmailAndReturnsOutcome()
    {
        _emailSender.IsSmtpConfigured = true;
        var handler = new CreateInvitationHandler(_currentUser, _identityGateway, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(
            new CreateInvitationCommand("new@example.com", token => $"https://trail.app/invite/{token}"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.True(result.Data.EmailSent);
        Assert.Equal("new@example.com", result.Data.Email);
        Assert.Contains("https://trail.app/invite/", result.Data.InvitationLink);

        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.Equal("new@example.com", sent.ToAddress);
        Assert.Equal(EmailKinds.Invitation, sent.Kind);
    }

    [Fact]
    public async Task CreateInvitation_SmtpNotConfigured_DoesNotSendEmailAndReturnsManualLink()
    {
        _emailSender.IsSmtpConfigured = false;
        var handler = new CreateInvitationHandler(_currentUser, _identityGateway, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(new CreateInvitationCommand("new@example.com"), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.False(result.Data.EmailSent);
        Assert.Contains("/Account/Register?inviteToken=", result.Data.InvitationLink);
        Assert.Empty(_emailSender.SentMessages);
    }

    [Fact]
    public async Task CreateInvitation_SmtpConfiguredSendFails_ReturnsOutcomeWithEmailSentFalse()
    {
        _emailSender.IsSmtpConfigured = true;
        _emailSender.SendResult = Result.Invalid(Message.Error("smtp.offline"));
        var handler = new CreateInvitationHandler(_currentUser, _identityGateway, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(new CreateInvitationCommand("new@example.com"), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.False(result.Data.EmailSent);
        Assert.NotEmpty(result.Data.InvitationLink);
    }

    [Fact]
    public async Task GenerateManualLink_NonAdmin_ReturnsForbidden()
    {
        _currentUser.IsAdmin = false;
        var handler = new GenerateManualAccountLinkHandler(_currentUser, _identityGateway);

        var result = await handler.HandleAsync(
            new GenerateManualAccountLinkCommand(UserId.New(), ManualLinkKind.EmailConfirmation),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task GenerateManualLink_UserNotFound_ReturnsNotFound()
    {
        var handler = new GenerateManualAccountLinkHandler(_currentUser, _identityGateway);

        var result = await handler.HandleAsync(
            new GenerateManualAccountLinkCommand(UserId.New(), ManualLinkKind.EmailConfirmation),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.admin.user_not_found", message.Code);
    }

    [Fact]
    public async Task GenerateManualLink_EmailConfirmationSucceeds_ReturnsLinkDto()
    {
        var targetId = UserId.New();
        _identityGateway.Users.Add(new AccountUserDto(targetId, "target@example.com", false, true, false, DateTime.UtcNow));
        var handler = new GenerateManualAccountLinkHandler(_currentUser, _identityGateway);

        var result = await handler.HandleAsync(new GenerateManualAccountLinkCommand(
            targetId,
            ManualLinkKind.EmailConfirmation,
            (id, token) => $"https://trail.app/manual-confirm/{id}/{token}"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal(targetId, result.Data.TargetUserId);
        Assert.Equal(ManualLinkKind.EmailConfirmation, result.Data.Kind);
        Assert.Equal("sample-confirm-token", result.Data.Token);
        Assert.Contains($"https://trail.app/manual-confirm/{targetId}/sample-confirm-token", result.Data.Url);
    }

    [Fact]
    public async Task GenerateManualLink_EmailConfirmationFails_ReturnsFailure()
    {
        var targetId = UserId.New();
        _identityGateway.Users.Add(new AccountUserDto(targetId, "target@example.com", false, true, false, DateTime.UtcNow));
        _identityGateway.EmailConfirmationTokenResult = Result.Invalid(Message.Error("token.error"));
        var handler = new GenerateManualAccountLinkHandler(_currentUser, _identityGateway);

        var result = await handler.HandleAsync(
            new GenerateManualAccountLinkCommand(targetId, ManualLinkKind.EmailConfirmation),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        var message = Assert.Single(result.Messages);
        Assert.Equal("token.error", message.Code);
    }

    [Fact]
    public async Task GenerateManualLink_PasswordResetSucceeds_ReturnsLinkDto()
    {
        var targetId = UserId.New();
        _identityGateway.Users.Add(new AccountUserDto(targetId, "target@example.com", false, true, true, DateTime.UtcNow));
        var handler = new GenerateManualAccountLinkHandler(_currentUser, _identityGateway);

        var result = await handler.HandleAsync(
            new GenerateManualAccountLinkCommand(targetId, ManualLinkKind.PasswordReset),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal(targetId, result.Data.TargetUserId);
        Assert.Equal(ManualLinkKind.PasswordReset, result.Data.Kind);
        Assert.Equal("sample-reset-token", result.Data.Token);
        Assert.Contains($"/Account/ResetPassword?userId={targetId}", result.Data.Url);
    }

    [Fact]
    public async Task GenerateManualLink_PasswordResetFails_ReturnsFailure()
    {
        var targetId = UserId.New();
        _identityGateway.Users.Add(new AccountUserDto(targetId, "target@example.com", false, true, true, DateTime.UtcNow));
        _identityGateway.PasswordResetTokenResult = Result.Invalid(Message.Error("reset.token.error"));
        var handler = new GenerateManualAccountLinkHandler(_currentUser, _identityGateway);

        var result = await handler.HandleAsync(
            new GenerateManualAccountLinkCommand(targetId, ManualLinkKind.PasswordReset),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        var message = Assert.Single(result.Messages);
        Assert.Equal("reset.token.error", message.Code);
    }

    [Fact]
    public async Task SendTestEmail_NonAdmin_ReturnsForbidden()
    {
        _currentUser.IsAdmin = false;
        var handler = new SendTestEmailHandler(_currentUser, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(
            new SendTestEmailCommand("test@example.com"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendTestEmail_MissingEmail_ReturnsInvalid(string? email)
    {
        var handler = new SendTestEmailHandler(_currentUser, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(
            new SendTestEmailCommand(email!),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.test_email.email_required", message.Code);
    }

    [Fact]
    public async Task SendTestEmail_SmtpNotConfigured_ReturnsInvalid()
    {
        _emailSender.IsSmtpConfigured = false;
        var handler = new SendTestEmailHandler(_currentUser, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(
            new SendTestEmailCommand("test@example.com"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.test_email.smtp_not_configured", message.Code);
    }

    [Fact]
    public async Task SendTestEmail_SmtpConfiguredSendSucceeds_ReturnsOk()
    {
        _emailSender.IsSmtpConfigured = true;
        var handler = new SendTestEmailHandler(_currentUser, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(
            new SendTestEmailCommand("test@example.com"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var message = Assert.Single(_emailSender.SentMessages);
        Assert.Equal("test@example.com", message.ToAddress);
        Assert.Equal(EmailKinds.Test, message.Kind);
    }

    [Fact]
    public async Task SendTestEmail_SendFails_ReturnsFailure()
    {
        _emailSender.IsSmtpConfigured = true;
        _emailSender.SendResult = Result.Invalid(Message.Error("smtp.failed"));
        var handler = new SendTestEmailHandler(_currentUser, _emailSender, _emailContent, _metrics);

        var result = await handler.HandleAsync(
            new SendTestEmailCommand("test@example.com"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        var message = Assert.Single(result.Messages);
        Assert.Equal("smtp.failed", message.Code);
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
