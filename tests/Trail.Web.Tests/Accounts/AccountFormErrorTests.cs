using System.Net;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Localization;
using Trail.Application.Accounts;
using Trail.Domain.Accounts;
using Trail.Domain.Common;
using Trail.Web.Htmx;

namespace Trail.Web.Tests.Accounts;

public sealed class AccountFormErrorTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    private readonly IStringLocalizer<SharedResource> _localizer = baseFactory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

    [Fact]
    public async Task Post_Register_WithPasswordBelowMinimumLength_Returns422AndShowsTheRuleUnderThePasswordField()
    {
        await using var factory = new TrailWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await AccountTestHelper.RegisterAsync(client, "first@trail.local", "abcde");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var document = await ParseAsync(response);
        Assert.NotNull(AccountTestHelper.InvalidField(document, "Input.Password"));
        Assert.Contains(_localizer["accounts.identity.password_too_short"].Value, AccountTestHelper.FieldErrors(document, "Input.Password"));
        Assert.Null(AccountTestHelper.InvalidField(document, "Input.Email"));
        Assert.Empty(AccountTestHelper.SummaryErrors(document));
    }

    [Fact]
    public async Task Post_Register_WithBlankEmail_Returns422AndShowsTheErrorUnderTheEmailField()
    {
        var currentUser = new TestCurrentUser { IsAuthenticated = false };
        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, new TestAccountIdentityGateway(), new TestEmailSender(), new TestUserSettingsStore(), currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await AccountTestHelper.RegisterAsync(client, " ", "SomePassword123!");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var document = await ParseAsync(response);
        Assert.Equal([_localizer["accounts.registration.email_required"].Value], AccountTestHelper.FieldErrors(document, "Input.Email"));
        Assert.NotNull(AccountTestHelper.InvalidField(document, "Input.Email"));
        Assert.Null(AccountTestHelper.InvalidField(document, "Input.Password"));
    }

    [Fact]
    public async Task Post_Login_WithBlankEmailAndPassword_Returns422AndShowsEachErrorUnderItsField()
    {
        var currentUser = new TestCurrentUser { IsAuthenticated = false };
        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, new TestAccountIdentityGateway(), new TestEmailSender(), new TestUserSettingsStore(), currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await AccountTestHelper.SignInAsync(client, " ", string.Empty);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var document = await ParseAsync(response);
        Assert.Equal([_localizer["accounts.login.email_required"].Value], AccountTestHelper.FieldErrors(document, "Input.Email"));
        Assert.Equal([_localizer["accounts.login.password_required"].Value], AccountTestHelper.FieldErrors(document, "Input.Password"));
        Assert.NotNull(AccountTestHelper.InvalidField(document, "Input.Email"));
        Assert.NotNull(AccountTestHelper.InvalidField(document, "Input.Password"));
        Assert.Empty(AccountTestHelper.SummaryErrors(document));
    }

    [Fact]
    public async Task Post_Login_WithInvalidCredentials_Returns422AndKeepsTheMessageInTheSummaryOnly()
    {
        var gateway = new TestAccountIdentityGateway { SignInResult = PasswordSignInResult.InvalidCredentials };
        var currentUser = new TestCurrentUser { IsAuthenticated = false };
        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, new TestEmailSender(), new TestUserSettingsStore(), currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await AccountTestHelper.SignInAsync(client, "someone@trail.local", "WrongPassword123!");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var document = await ParseAsync(response);
        Assert.Equal([_localizer["accounts.login.invalid_credentials"].Value], AccountTestHelper.SummaryErrors(document));
        Assert.Empty(document.QuerySelectorAll("[aria-invalid='true']"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Post_ChangePassword_WithWrongCurrentPassword_Returns422AndShowsTheErrorUnderTheCurrentPasswordField(bool htmx)
    {
        var gateway = new TestAccountIdentityGateway
        {
            ChangePasswordResult = Result.Invalid(Message.Error("accounts.identity.password_mismatch", "CurrentPassword")),
        };
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, new TestEmailSender(), new TestUserSettingsStore(), currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Settings");

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Settings?handler=Password",
            new Dictionary<string, string>
            {
                ["PasswordInput.CurrentPassword"] = "WrongPassword123!",
                ["PasswordInput.NewPassword"] = "NewPassword123!",
                ["PasswordInput.ConfirmPassword"] = "NewPassword123!",
            },
            csrfHeader,
            csrfToken,
            htmx ? new Dictionary<string, string> { [HtmxIntegration.RequestTypeHeaderName] = HtmxIntegration.PartialRequestType } : null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var document = await ParseAsync(response);
        Assert.Equal(!htmx, document.QuerySelector("main#main-content") is not null);
        Assert.NotNull(AccountTestHelper.InvalidField(document, "PasswordInput.CurrentPassword"));
        Assert.Equal([_localizer["accounts.identity.password_mismatch"].Value], AccountTestHelper.FieldErrors(document, "PasswordInput.CurrentPassword"));
        Assert.Null(AccountTestHelper.InvalidField(document, "PasswordInput.NewPassword"));
        Assert.Empty(AccountTestHelper.SummaryErrors(document));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Post_ChangePassword_WithAFormLevelError_ShowsItOnceInTheSummaryAndNeverAsAToast(bool htmx)
    {
        var gateway = new TestAccountIdentityGateway
        {
            ChangePasswordResult = Result.Invalid(Message.Error("accounts.identity.password_mismatch")),
        };
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, new TestEmailSender(), new TestUserSettingsStore(), currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Settings");

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Settings?handler=Password",
            new Dictionary<string, string>
            {
                ["PasswordInput.CurrentPassword"] = "WrongPassword123!",
                ["PasswordInput.NewPassword"] = "NewPassword123!",
                ["PasswordInput.ConfirmPassword"] = "NewPassword123!",
            },
            csrfHeader,
            csrfToken,
            htmx ? new Dictionary<string, string> { [HtmxIntegration.RequestTypeHeaderName] = HtmxIntegration.PartialRequestType } : null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);
        var message = _localizer["accounts.identity.password_mismatch"].Value;
        Assert.Equal([message], AccountTestHelper.SummaryErrors(document));
        Assert.Equal(1, html.Split(message, StringSplitOptions.None).Length - 1);
        Assert.Empty(document.QuerySelectorAll("[data-toast]"));
    }

    [Fact]
    public async Task Post_ChangePassword_WithMismatchedConfirmation_ShowsTheErrorUnderTheConfirmationField()
    {
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, new TestAccountIdentityGateway(), new TestEmailSender(), new TestUserSettingsStore(), currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Settings");

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Settings?handler=Password",
            new Dictionary<string, string>
            {
                ["PasswordInput.CurrentPassword"] = "OldPassword123!",
                ["PasswordInput.NewPassword"] = "NewPassword123!",
                ["PasswordInput.ConfirmPassword"] = "DifferentPassword123!",
            },
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var document = await ParseAsync(response);
        Assert.NotNull(AccountTestHelper.InvalidField(document, "PasswordInput.ConfirmPassword"));
        Assert.Equal([_localizer["accounts.change_password.passwords_do_not_match"].Value], AccountTestHelper.FieldErrors(document, "PasswordInput.ConfirmPassword"));
        Assert.Empty(AccountTestHelper.SummaryErrors(document));
    }

    [Theory]
    [InlineData("100.0", false)]
    [InlineData("1e2", true)]
    public async Task Post_Settings_WithAFocusValueThatCannotBeParsed_Returns422AndKeepsTheStoredSettings(string typed, bool htmx)
    {
        var userId = UserId.New();
        var settingsStore = new TestUserSettingsStore { Settings = UserSettings.CreateDefault(userId) };
        Assert.True(settingsStore.Settings.Update("America/Sao_Paulo", 50, 5).IsOk);
        var currentUser = new TestCurrentUser { IsAuthenticated = true, Id = userId };
        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, new TestAccountIdentityGateway(), new TestEmailSender(), settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Settings");

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Settings?handler=Settings",
            new Dictionary<string, string>
            {
                ["SettingsInput.TimeZoneId"] = "America/Sao_Paulo",
                ["SettingsInput.PomodoroFocusMinutes"] = typed,
                ["SettingsInput.PomodoroBreakMinutes"] = "5",
            },
            csrfHeader,
            csrfToken,
            htmx ? new Dictionary<string, string> { [HtmxIntegration.RequestTypeHeaderName] = HtmxIntegration.PartialRequestType } : null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(50, settingsStore.Settings.PomodoroFocusMinutes);
        Assert.False(settingsStore.Saved);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);
        Assert.Equal(typed, AccountTestHelper.InvalidField(document, "SettingsInput.PomodoroFocusMinutes")!.GetAttribute("value"));
        Assert.Equal([_localizer["ModelBindingAttemptedValueIsInvalid", typed].Value], AccountTestHelper.FieldErrors(document, "SettingsInput.PomodoroFocusMinutes"));
        Assert.Equal(htmx, document.QuerySelector("main#main-content") is null);
        Assert.NotNull(document.QuerySelector("#settings-form-container"));
        Assert.Null(document.GetElementById("binding-errors"));
        Assert.Empty(document.QuerySelectorAll("[data-toast]"));
    }

    [Fact]
    public async Task Post_HtmxSettings_WithValidValues_RendersTheFormWithoutClientValidationAttributes()
    {
        var userId = UserId.New();
        var settingsStore = new TestUserSettingsStore { Settings = UserSettings.CreateDefault(userId) };
        var currentUser = new TestCurrentUser { IsAuthenticated = true, Id = userId };
        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, new TestAccountIdentityGateway(), new TestEmailSender(), settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Settings");

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Settings?handler=Settings",
            new Dictionary<string, string>
            {
                ["SettingsInput.TimeZoneId"] = "America/Sao_Paulo",
                ["SettingsInput.PomodoroFocusMinutes"] = "30",
                ["SettingsInput.PomodoroBreakMinutes"] = "6",
            },
            csrfHeader,
            csrfToken,
            new Dictionary<string, string> { [HtmxIntegration.RequestTypeHeaderName] = HtmxIntegration.PartialRequestType });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("SettingsInput.PomodoroFocusMinutes", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-val", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Post_AdminToggleActive_WithAnUnparsableFlag_Returns422ShowsTheMessageOnceAndKeepsTheUserActive(bool htmx)
    {
        var adminId = UserId.New();
        var memberId = UserId.New();
        var gateway = new TestAccountIdentityGateway();
        gateway.Users.Add(new AccountUserDto(adminId, "admin@example.com", true, true, true, DateTime.UtcNow));
        gateway.Users.Add(new AccountUserDto(memberId, "member@example.com", false, true, true, DateTime.UtcNow));
        var currentUser = new TestCurrentUser { IsAuthenticated = true, IsAdmin = true, Id = adminId };
        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, new TestEmailSender(), new TestUserSettingsStore(), currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Admin/Users");

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Admin/Users?handler=ToggleActive",
            new Dictionary<string, string> { ["userId"] = memberId.Value.ToString(), ["isActive"] = "xyz" },
            csrfHeader,
            csrfToken,
            htmx ? new Dictionary<string, string> { [HtmxIntegration.RequestTypeHeaderName] = HtmxIntegration.PartialRequestType } : null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.True(gateway.Users.Single(user => user.Id == memberId).IsActive);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);
        var message = _localizer["ModelBindingAttemptedValueIsInvalid", "xyz"].Value;
        Assert.Equal(1, document.DocumentElement.TextContent.Split(message, StringSplitOptions.None).Length - 1);
        Assert.Equal(htmx, document.QuerySelector("[data-toast]") is not null);
        Assert.Equal(!htmx, document.GetElementById("binding-errors") is not null);
    }

    [Fact]
    public async Task Post_AddSection_ToAPlanThatDoesNotExistWithAnUnparsableVersion_Returns404()
    {
        var currentUser = new TestCurrentUser { IsAuthenticated = true };
        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, new TestAccountIdentityGateway(), new TestEmailSender(), new TestUserSettingsStore(), currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Settings");

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            $"/planos/{Guid.NewGuid()}?handler=AddSection&expectedPlanVersion=abc",
            new Dictionary<string, string> { ["name"] = "Section" },
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_AdminToggleActive_OnOwnAccount_Returns422AndShowsTheErrorInTheSummary()
    {
        var adminId = UserId.New();
        var gateway = new TestAccountIdentityGateway();
        gateway.Users.Add(new AccountUserDto(adminId, "admin@example.com", true, true, true, DateTime.UtcNow));
        var currentUser = new TestCurrentUser { IsAuthenticated = true, IsAdmin = true, Id = adminId };
        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, new TestEmailSender(), new TestUserSettingsStore(), currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Admin/Users");

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Admin/Users?handler=ToggleActive",
            new Dictionary<string, string> { ["userId"] = adminId.Value.ToString(), ["isActive"] = "false" },
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var document = await ParseAsync(response);
        Assert.Equal([_localizer["accounts.admin.cannot_deactivate_self"].Value], AccountTestHelper.SummaryErrors(document));
        Assert.Empty(document.QuerySelectorAll("[aria-invalid='true']"));
    }

    [Fact]
    public async Task Post_AdminTestEmail_WithoutRecipient_Returns422AndShowsTheErrorUnderTheRecipientField()
    {
        var adminId = UserId.New();
        var gateway = new TestAccountIdentityGateway();
        gateway.Users.Add(new AccountUserDto(adminId, "admin@example.com", true, true, true, DateTime.UtcNow));
        var currentUser = new TestCurrentUser { IsAuthenticated = true, IsAdmin = true, Id = adminId };
        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, new TestEmailSender(), new TestUserSettingsStore(), currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Admin/Users");

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Admin/Users?handler=TestEmail",
            new Dictionary<string, string> { ["recipientEmail"] = " " },
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var document = await ParseAsync(response);
        Assert.NotNull(AccountTestHelper.InvalidField(document, "recipientEmail"));
        Assert.Equal([_localizer["accounts.test_email.email_required"].Value], AccountTestHelper.FieldErrors(document, "recipientEmail"));
        Assert.Null(AccountTestHelper.InvalidField(document, "email"));
        Assert.Empty(AccountTestHelper.SummaryErrors(document));
    }

    private static async Task<IHtmlDocument> ParseAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return new HtmlParser().ParseDocument(html);
    }
}
