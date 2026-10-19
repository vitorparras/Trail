using Trail.Application.Accounts;
using Trail.Domain.Common;

namespace Trail.Application.Tests.Accounts;

public sealed class RequestPasswordResetHandlerTests
{
    private readonly FakeAccountIdentityGateway _identityGateway = new();
    private readonly FakeEmailSender _emailSender = new();
    private readonly FakeAccountEmailDelivery _emailDelivery = new();

    private RequestPasswordResetHandler CreateHandler() =>
        new(_identityGateway, _emailSender, new FallbackAccountEmailContent(), _emailDelivery);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_MissingEmail_ReturnsInvalid(string? email)
    {
        var handler = CreateHandler();
        var command = new RequestPasswordResetCommand(email!);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.password_reset.email_required", message.Code);
    }

    [Fact]
    public async Task HandleAsync_UserNotFound_ReturnsOkToPreventEnumeration()
    {
        var handler = CreateHandler();
        var command = new RequestPasswordResetCommand("notfound@example.com");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(_emailDelivery.Scheduled);
    }

    [Fact]
    public async Task HandleAsync_UserInactive_ReturnsOkToPreventEnumeration()
    {
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "inactive@example.com", false, false, true, DateTime.UtcNow));
        var handler = CreateHandler();
        var command = new RequestPasswordResetCommand("inactive@example.com");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(_emailDelivery.Scheduled);
    }

    [Fact]
    public async Task HandleAsync_SmtpNotConfigured_ReturnsOk()
    {
        _emailSender.IsSmtpConfigured = false;
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "user@example.com", false, true, true, DateTime.UtcNow));
        var handler = CreateHandler();
        var command = new RequestPasswordResetCommand("user@example.com");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(_emailDelivery.Scheduled);
    }

    [Fact]
    public async Task HandleAsync_TokenGenerationFails_ReturnsTheSameOkAsAnUnknownEmail()
    {
        _identityGateway.PasswordResetTokenResult = Result.Invalid(Message.Error("token.error"));
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "user@example.com", false, true, true, DateTime.UtcNow));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(new RequestPasswordResetCommand("user@example.com"), TestContext.Current.CancellationToken);
        var unknown = await handler.HandleAsync(new RequestPasswordResetCommand("unknown@example.com"), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(result.Messages);
        Assert.Equal(unknown.Status, result.Status);
        Assert.Empty(_emailDelivery.Scheduled);
    }

    [Fact]
    public async Task HandleAsync_KnownEmail_QueuesTheEmailWithoutCallingTheSenderAndAnswersAsForAnUnknownEmail()
    {
        _emailSender.SendResult = Result.Invalid(Message.Error("smtp.error"));
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "user@example.com", false, true, true, DateTime.UtcNow));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(new RequestPasswordResetCommand("user@example.com"), TestContext.Current.CancellationToken);
        var unknown = await handler.HandleAsync(new RequestPasswordResetCommand("unknown@example.com"), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(result.Messages);
        Assert.Equal(unknown.Status, result.Status);
        Assert.Equal("user@example.com", Assert.Single(_emailDelivery.Scheduled).ToAddress);
        Assert.Empty(_emailSender.SentMessages);
    }

    [Fact]
    public async Task HandleAsync_EmailSendSucceeds_SendsEmailAndReturnsOk()
    {
        var userId = UserId.New();
        _identityGateway.Users.Add(new AccountUserDto(userId, "user@example.com", false, true, true, DateTime.UtcNow));
        var handler = CreateHandler();
        var command = new RequestPasswordResetCommand(
            "user@example.com",
            ResetLinkBuilder: (id, token) => $"https://trail.app/reset/{id}/{token}");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var message = Assert.Single(_emailDelivery.Scheduled);
        Assert.Equal("user@example.com", message.ToAddress);
        Assert.Equal(EmailKinds.PasswordReset, message.Kind);
        Assert.Contains($"https://trail.app/reset/{userId}/sample-reset-token", message.HtmlBody);
    }
}
