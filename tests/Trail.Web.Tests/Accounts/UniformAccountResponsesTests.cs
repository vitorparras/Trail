using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Localization;
using Trail.Application.Accounts;
using Trail.Domain.Common;

namespace Trail.Web.Tests.Accounts;

public sealed class OpenRegistrationWebApplicationFactory : TrailWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Registration:Mode"] = "Open",
            ["Security:AuthRateLimitPermitLimit"] = "1000",
        }));
    }
}

public sealed partial class UniformAccountResponsesTests(OpenRegistrationWebApplicationFactory factory) : IClassFixture<OpenRegistrationWebApplicationFactory>
{
    private const string ValidPassword = "ValidPassword123!";
    private const string WrongPassword = "WrongPassword123!";

    [Theory]
    [InlineData(ValidPassword, HttpStatusCode.OK)]
    [InlineData("abcde", HttpStatusCode.UnprocessableEntity)]
    public async Task Post_Register_ForExistingAndNewEmail_AnswersWithTheSameStatusAndText(string password, HttpStatusCode expectedStatus)
    {
        var existingEmail = await CreateUserAsync();

        var existing = await RegisterAsync(existingEmail, password);
        var created = await RegisterAsync($"new-{Guid.NewGuid():N}@trail.local", password);

        Assert.Equal(expectedStatus, existing.StatusCode);
        Assert.Equal(existing, created);
    }

    [Fact]
    public async Task Post_ForgotPassword_ForUnknownEmailAndFailedDelivery_AnswersWithTheSameStatusAndText()
    {
        var gateway = new TestAccountIdentityGateway();
        gateway.Users.Add(new AccountUserDto(UserId.New(), "known@example.com", false, true, true, DateTime.UtcNow));
        var failingSender = new TestEmailSender { SendResult = Result.Invalid(Message.Error("accounts.email.send_failed")) };
        using var appFactory = AccountTestHelper.CreateTestFactory(factory, gateway, failingSender, new TestUserSettingsStore(), new TestCurrentUser { IsAuthenticated = false });

        var unknown = await PostEmailFormAsync(appFactory, "/Account/ForgotPassword", "unknown@example.com");
        var failedDelivery = await PostEmailFormAsync(appFactory, "/Account/ForgotPassword", "known@example.com");

        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(unknown, failedDelivery);
        Assert.Single(await failingSender.WaitForMessagesAsync(1));
    }

    [Fact]
    public async Task Post_ResendConfirmation_ForUnknownEmailAndFailedDelivery_AnswersWithTheSameStatusAndText()
    {
        var gateway = new TestAccountIdentityGateway();
        gateway.Users.Add(new AccountUserDto(UserId.New(), "pending@example.com", false, true, false, DateTime.UtcNow));
        var failingSender = new TestEmailSender { SendResult = Result.Invalid(Message.Error("accounts.email.send_failed")) };
        using var appFactory = AccountTestHelper.CreateTestFactory(factory, gateway, failingSender, new TestUserSettingsStore(), new TestCurrentUser { IsAuthenticated = false });

        var unknown = await PostEmailFormAsync(appFactory, "/Account/ResendConfirmation", "unknown@example.com");
        var failedDelivery = await PostEmailFormAsync(appFactory, "/Account/ResendConfirmation", "pending@example.com");

        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(unknown, failedDelivery);
        Assert.Single(await failingSender.WaitForMessagesAsync(1));
    }

    [Fact]
    public async Task Post_ResetPassword_ForUnknownUserAndInvalidToken_AnswersWithTheSameStatusAndText()
    {
        var userId = await UserIdOfAsync(await CreateUserAsync());

        var unknownUser = await ResetPasswordAsync(Guid.NewGuid(), "invalid-token");
        var invalidToken = await ResetPasswordAsync(userId, "invalid-token");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknownUser.StatusCode);
        Assert.Equal(unknownUser, invalidToken);
    }

    [Fact]
    public async Task Get_ConfirmEmail_ForUnknownUserAndInvalidToken_AnswersWithTheSameStatusAndText()
    {
        var userId = await UserIdOfAsync(await CreateUserAsync(emailConfirmed: false));
        using var client = CreateClient();

        var unknownUser = await ReadAsync(await client.GetAsync($"/Account/ConfirmEmail?userId={Guid.NewGuid()}&token=invalid-token", TestContext.Current.CancellationToken));
        var invalidToken = await ReadAsync(await client.GetAsync($"/Account/ConfirmEmail?userId={userId}&token=invalid-token", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknownUser.StatusCode);
        Assert.Equal(unknownUser, invalidToken);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task Post_Login_WithAWrongPasswordForAnExistingEmail_AnswersAsForAnUnknownEmail(bool emailConfirmed, bool isActive)
    {
        var existingEmail = await CreateUserAsync(emailConfirmed, isActive);

        var unknown = await LoginAsync($"unknown-{Guid.NewGuid():N}@trail.local", WrongPassword);
        var existing = await LoginAsync(existingEmail, WrongPassword);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        Assert.Contains(Text("accounts.login.invalid_credentials"), unknown.MainText, StringComparison.Ordinal);
        Assert.Equal(unknown, existing);
    }

    [Theory]
    [InlineData(false, true, "accounts.login.email_not_confirmed")]
    [InlineData(true, false, "accounts.login.deactivated")]
    [InlineData(false, false, "accounts.login.deactivated")]
    public async Task Post_Login_WithTheCorrectPassword_ReportsTheAccountState(bool emailConfirmed, bool isActive, string expectedCode)
    {
        var email = await CreateUserAsync(emailConfirmed, isActive);

        var response = await LoginAsync(email, ValidPassword);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(Text(expectedCode), response.MainText, StringComparison.Ordinal);
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private string Text(string key) =>
        factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>()[key].Value;

    private async Task<string> CreateUserAsync(bool emailConfirmed = true, bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IAccountIdentityGateway>();
        var email = $"existing-{Guid.NewGuid():N}@trail.local";

        var created = await gateway.CreateUserAsync(email, ValidPassword, isAdmin: false, emailConfirmed, TestContext.Current.CancellationToken);
        Assert.True(created.IsOk);
        if (!isActive)
        {
            Assert.True((await gateway.SetUserActiveStatusAsync(created.Data, isActive: false, TestContext.Current.CancellationToken)).IsOk);
        }

        return email;
    }

    private async Task<AccountResponse> LoginAsync(string email, string password)
    {
        using var client = CreateClient();

        return await ReadAsync(await AccountTestHelper.SignInAsync(client, email, password, rememberMe: false));
    }

    private async Task<Guid> UserIdOfAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IAccountIdentityGateway>();

        var user = await gateway.GetUserByEmailAsync(email, TestContext.Current.CancellationToken);
        Assert.NotNull(user);

        return user.Id.Value;
    }

    private async Task<AccountResponse> RegisterAsync(string email, string password)
    {
        using var client = CreateClient();

        return await ReadAsync(await AccountTestHelper.RegisterAsync(client, email, password));
    }

    private static async Task<AccountResponse> PostEmailFormAsync(WebApplicationFactory<Program> appFactory, string path, string email)
    {
        using var client = appFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, page, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, path);
        var form = WithFormToken(page, new Dictionary<string, string> { ["Email"] = email });

        return await ReadAsync(await AccountTestHelper.PostFormWithAntiforgeryAsync(client, path, form, csrfHeader, csrfToken));
    }

    private async Task<AccountResponse> ResetPasswordAsync(Guid userId, string token)
    {
        using var client = CreateClient();
        var (_, page, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, $"/Account/ResetPassword?userId={userId}&token={token}");
        var form = WithFormToken(page, new Dictionary<string, string>
        {
            ["Input.UserId"] = userId.ToString(),
            ["Input.Token"] = token,
            ["Input.NewPassword"] = ValidPassword,
            ["Input.ConfirmNewPassword"] = ValidPassword,
        });

        return await ReadAsync(await AccountTestHelper.PostFormWithAntiforgeryAsync(client, "/Account/ResetPassword", form, csrfHeader, csrfToken));
    }

    private static Dictionary<string, string> WithFormToken(AngleSharp.Html.Dom.IHtmlDocument page, Dictionary<string, string> form)
    {
        var formToken = page.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");
        if (!string.IsNullOrEmpty(formToken))
        {
            form["__RequestVerificationToken"] = formToken;
        }

        return form;
    }

    private static async Task<AccountResponse> ReadAsync(HttpResponseMessage response)
    {
        using (response)
        {
            var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var main = new HtmlParser().ParseDocument(html).QuerySelector("main")?.TextContent ?? string.Empty;

            return new AccountResponse(response.StatusCode, Whitespace().Replace(main, " ").Trim());
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private sealed record AccountResponse(HttpStatusCode StatusCode, string MainText);
}
