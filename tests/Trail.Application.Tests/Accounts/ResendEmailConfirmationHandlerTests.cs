using Trail.Application.Accounts;
using Trail.Domain.Common;

namespace Trail.Application.Tests.Accounts;

public sealed class ResendEmailConfirmationHandlerTests
{
    private readonly FakeAccountIdentityGateway _identityGateway = new();
    private readonly FakeEmailSender _emailSender = new();
    private readonly FakeAccountEmailDelivery _emailDelivery = new();

    private ResendEmailConfirmationHandler CreateHandler() =>
        new(_identityGateway, _emailSender, new FallbackAccountEmailContent(), _emailDelivery);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_MissingEmail_ReturnsInvalid(string? email)
    {
        var handler = CreateHandler();
        var command = new ResendEmailConfirmationCommand(email!);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.resend_confirmation.email_required", message.Code);
    }

    [Fact]
    public async Task HandleAsync_UserNotFound_ReturnsOkToPreventEnumeration()
    {
        var handler = CreateHandler();
        var command = new ResendEmailConfirmationCommand("notfound@example.com");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(_emailDelivery.Scheduled);
    }

    [Fact]
    public async Task HandleAsync_UserAlreadyConfirmed_ReturnsOk()
    {
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "confirmed@example.com", false, true, true, DateTime.UtcNow));
        var handler = CreateHandler();
        var command = new ResendEmailConfirmationCommand("confirmed@example.com");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(_emailDelivery.Scheduled);
    }

    [Fact]
    public async Task HandleAsync_UserInactive_ReturnsOk()
    {
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "inactive@example.com", false, false, false, DateTime.UtcNow));
        var handler = CreateHandler();
        var command = new ResendEmailConfirmationCommand("inactive@example.com");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(_emailDelivery.Scheduled);
    }

    [Fact]
    public async Task HandleAsync_SmtpNotConfigured_ReturnsOk()
    {
        _emailSender.IsSmtpConfigured = false;
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "pending@example.com", false, true, false, DateTime.UtcNow));
        var handler = CreateHandler();
        var command = new ResendEmailConfirmationCommand("pending@example.com");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(_emailDelivery.Scheduled);
    }

    [Fact]
    public async Task HandleAsync_TokenGenerationFails_ReturnsTheSameOkAsAnUnknownEmail()
    {
        _identityGateway.EmailConfirmationTokenResult = Result.Invalid(Message.Error("token.error"));
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "pending@example.com", false, true, false, DateTime.UtcNow));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(new ResendEmailConfirmationCommand("pending@example.com"), TestContext.Current.CancellationToken);
        var unknown = await handler.HandleAsync(new ResendEmailConfirmationCommand("unknown@example.com"), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(result.Messages);
        Assert.Equal(unknown.Status, result.Status);
        Assert.Empty(_emailDelivery.Scheduled);
    }

    [Fact]
    public async Task HandleAsync_KnownEmail_QueuesTheEmailWithoutCallingTheSenderAndAnswersAsForAnUnknownEmail()
    {
        _emailSender.SendResult = Result.Invalid(Message.Error("smtp.error"));
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "pending@example.com", false, true, false, DateTime.UtcNow));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(new ResendEmailConfirmationCommand("pending@example.com"), TestContext.Current.CancellationToken);
        var unknown = await handler.HandleAsync(new ResendEmailConfirmationCommand("unknown@example.com"), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(result.Messages);
        Assert.Equal(unknown.Status, result.Status);
        Assert.Equal("pending@example.com", Assert.Single(_emailDelivery.Scheduled).ToAddress);
        Assert.Empty(_emailSender.SentMessages);
    }

    [Fact]
    public async Task HandleAsync_SendEmailSucceeds_SendsEmailAndReturnsOk()
    {
        var userId = UserId.New();
        _identityGateway.Users.Add(new AccountUserDto(userId, "pending@example.com", false, true, false, DateTime.UtcNow));
        var handler = CreateHandler();
        var command = new ResendEmailConfirmationCommand(
            "pending@example.com",
            ConfirmationLinkBuilder: (id, token) => $"https://trail.app/confirm/{id}/{token}");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var message = Assert.Single(_emailDelivery.Scheduled);
        Assert.Equal("pending@example.com", message.ToAddress);
        Assert.Contains($"https://trail.app/confirm/{userId}/sample-confirm-token", message.HtmlBody);
    }
}
