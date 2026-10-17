using Trail.Application.Accounts;
using Trail.Domain.Common;

namespace Trail.Application.Tests.Accounts;

public sealed class LoginHandlerTests
{
    private readonly FakeAccountIdentityGateway _identityGateway = new();

    private LoginHandler CreateHandler() => new(_identityGateway);

    [Theory]
    [InlineData(null, "Password123!", "accounts.login.email_required")]
    [InlineData("", "Password123!", "accounts.login.email_required")]
    [InlineData("user@example.com", null, "accounts.login.password_required")]
    [InlineData("user@example.com", "", "accounts.login.password_required")]
    public async Task HandleAsync_ValidationErrors_ReturnsInvalidWithExpectedCode(string? email, string? password, string expectedCode)
    {
        var handler = CreateHandler();
        var command = new LoginCommand(email!, password!);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Contains(result.Messages, m => m.Code == expectedCode);
    }

    [Fact]
    public async Task HandleAsync_SuccessfulSignIn_ReturnsOk()
    {
        _identityGateway.SignInResult = PasswordSignInResult.Success;
        var handler = CreateHandler();
        var command = new LoginCommand("user@example.com", "Password123!", RememberMe: true);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
    }

    [Fact]
    public async Task HandleAsync_InvalidCredentials_ReturnsInvalid()
    {
        _identityGateway.SignInResult = PasswordSignInResult.InvalidCredentials;
        var handler = CreateHandler();
        var command = new LoginCommand("user@example.com", "WrongPassword");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.login.invalid_credentials", message.Code);
    }

    [Fact]
    public async Task HandleAsync_LockedOut_ReturnsForbidden()
    {
        _identityGateway.SignInResult = PasswordSignInResult.LockedOut;
        var handler = CreateHandler();
        var command = new LoginCommand("user@example.com", "Password123!");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.login.locked_out", message.Code);
    }

    [Fact]
    public async Task HandleAsync_EmailNotConfirmed_ReturnsForbidden()
    {
        _identityGateway.SignInResult = PasswordSignInResult.EmailNotConfirmed;
        var handler = CreateHandler();
        var command = new LoginCommand("user@example.com", "Password123!");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.login.email_not_confirmed", message.Code);
    }

    [Fact]
    public async Task HandleAsync_Deactivated_ReturnsForbidden()
    {
        _identityGateway.SignInResult = PasswordSignInResult.Deactivated;
        var handler = CreateHandler();
        var command = new LoginCommand("user@example.com", "Password123!");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.login.deactivated", message.Code);
    }
}
