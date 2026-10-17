using Trail.Application.Accounts;

namespace Trail.Application.Tests.Accounts;

public sealed class LogoutHandlerTests
{
    [Fact]
    public async Task HandleAsync_CallsSignOutAndReturnsOk()
    {
        var gateway = new FakeAccountIdentityGateway();
        var handler = new LogoutHandler(gateway);

        var result = await handler.HandleAsync(new LogoutCommand(), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.True(gateway.SignedOut);
    }
}
