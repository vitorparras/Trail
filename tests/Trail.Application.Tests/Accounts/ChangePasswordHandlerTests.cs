using Trail.Application.Accounts;
using Trail.Domain.Common;

namespace Trail.Application.Tests.Accounts;

public sealed class ChangePasswordHandlerTests
{
    private readonly TestCurrentUser _currentUser = new();
    private readonly FakeAccountIdentityGateway _identityGateway = new();

    private ChangePasswordHandler CreateHandler() => new(_currentUser, _identityGateway);

    [Fact]
    public async Task HandleAsync_UnauthenticatedUser_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = CreateHandler();
        var command = new ChangePasswordCommand("OldPass1!", "NewPass1!", "NewPass1!");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.auth.unauthenticated", message.Code);
    }

    [Theory]
    [InlineData(null, "NewPass1!", "NewPass1!", "accounts.change_password.current_password_required")]
    [InlineData("", "NewPass1!", "NewPass1!", "accounts.change_password.current_password_required")]
    [InlineData("OldPass1!", null, "NewPass1!", "accounts.change_password.new_password_required")]
    [InlineData("OldPass1!", "", "", "accounts.change_password.new_password_required")]
    [InlineData("OldPass1!", "NewPass1!", "Mismatch!", "accounts.change_password.passwords_do_not_match")]
    public async Task HandleAsync_ValidationErrors_ReturnsInvalidWithExpectedCode(
        string? currentPassword,
        string? newPassword,
        string? confirmNewPassword,
        string expectedCode)
    {
        var handler = CreateHandler();
        var command = new ChangePasswordCommand(currentPassword!, newPassword!, confirmNewPassword!);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains(result.Messages, m => m.Code == expectedCode);
    }

    [Fact]
    public async Task HandleAsync_ValidPasswords_CallsGatewayAndReturnsOk()
    {
        var handler = CreateHandler();
        var command = new ChangePasswordCommand("OldPass1!", "NewPass1!", "NewPass1!");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
    }

    [Fact]
    public async Task HandleAsync_GatewayFails_ReturnsFailure()
    {
        _identityGateway.ChangePasswordResult = Result.Invalid(Message.Error("identity.incorrect_password"));
        var handler = CreateHandler();
        var command = new ChangePasswordCommand("WrongPass1!", "NewPass1!", "NewPass1!");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        var message = Assert.Single(result.Messages);
        Assert.Equal("identity.incorrect_password", message.Code);
    }
}
