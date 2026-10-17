using Trail.Application.Accounts;
using Trail.Domain.Common;

namespace Trail.Application.Tests.Accounts;

public sealed class ConfirmEmailHandlerTests
{
    private readonly FakeAccountIdentityGateway _identityGateway = new();

    private ConfirmEmailHandler CreateHandler() => new(_identityGateway);

    [Fact]
    public async Task HandleAsync_ValidUserIdAndToken_CallsGatewayAndReturnsSuccess()
    {
        var handler = CreateHandler();
        var userId = UserId.New();
        var command = new ConfirmEmailCommand(userId, "valid-token");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
    }

    [Fact]
    public async Task HandleAsync_EmptyUserId_ReturnsInvalid()
    {
        var handler = CreateHandler();
        var command = new ConfirmEmailCommand(new UserId(Guid.Empty), "valid-token");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.confirm_email.invalid_token", message.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_MissingOrWhitespaceToken_ReturnsInvalid(string? token)
    {
        var handler = CreateHandler();
        var command = new ConfirmEmailCommand(UserId.New(), token!);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.confirm_email.invalid_token", message.Code);
    }

    [Fact]
    public async Task HandleAsync_GatewayFailsConfirmation_ReturnsGatewayFailure()
    {
        _identityGateway.ConfirmEmailResult = Result.Invalid(Message.Error("identity.invalid_token"));
        var handler = CreateHandler();
        var command = new ConfirmEmailCommand(UserId.New(), "invalid-token");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        var message = Assert.Single(result.Messages);
        Assert.Equal("identity.invalid_token", message.Code);
    }
}
