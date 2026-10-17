using Trail.Application.Accounts;
using Trail.Domain.Common;

namespace Trail.Application.Tests.Accounts;

public sealed class ResetPasswordHandlerTests
{
    private readonly FakeAccountIdentityGateway _identityGateway = new();

    private ResetPasswordHandler CreateHandler() => new(_identityGateway);

    [Fact]
    public async Task HandleAsync_EmptyUserId_ReturnsInvalid()
    {
        var handler = CreateHandler();
        var command = new ResetPasswordCommand(new UserId(Guid.Empty), "valid-token", "NewPass1!", "NewPass1!");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains(result.Messages, m => m.Code == "accounts.password_reset.invalid_user");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_MissingToken_ReturnsInvalid(string? token)
    {
        var handler = CreateHandler();
        var command = new ResetPasswordCommand(UserId.New(), token!, "NewPass1!", "NewPass1!");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains(result.Messages, m => m.Code == "accounts.password_reset.invalid_token");
    }

    [Theory]
    [InlineData(null, "NewPass1!", "accounts.password_reset.password_required")]
    [InlineData("", "", "accounts.password_reset.password_required")]
    [InlineData("NewPass1!", "Different1!", "accounts.password_reset.passwords_do_not_match")]
    public async Task HandleAsync_PasswordValidationErrors_ReturnsInvalidWithExpectedCode(
        string? newPassword,
        string? confirmNewPassword,
        string expectedCode)
    {
        var handler = CreateHandler();
        var command = new ResetPasswordCommand(UserId.New(), "valid-token", newPassword!, confirmNewPassword!);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains(result.Messages, m => m.Code == expectedCode);
    }

    [Fact]
    public async Task HandleAsync_ValidParameters_CallsGatewayAndReturnsOk()
    {
        var handler = CreateHandler();
        var command = new ResetPasswordCommand(UserId.New(), "valid-token", "NewPass1!", "NewPass1!");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
    }

    [Fact]
    public async Task HandleAsync_GatewayFails_ReturnsFailure()
    {
        _identityGateway.ResetPasswordResult = Result.Invalid(Message.Error("identity.token_expired"));
        var handler = CreateHandler();
        var command = new ResetPasswordCommand(UserId.New(), "expired-token", "NewPass1!", "NewPass1!");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        var message = Assert.Single(result.Messages);
        Assert.Equal("identity.token_expired", message.Code);
    }
}
