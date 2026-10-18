using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Trail.Application.Accounts;
using Trail.Domain.Accounts;
using Trail.Domain.Common;

namespace Trail.Web.Tests.Accounts;

public sealed class AccountPagesTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    [Fact]
    public async Task Get_Login_WhenUnauthenticated_ReturnsOkAndRendersLoginForm()
    {
        var gateway = new TestAccountIdentityGateway();
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (response, document, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(document.QuerySelector("input[name='Input.Email']"));
        Assert.NotNull(document.QuerySelector("input[name='Input.Password']"));
        Assert.NotNull(document.QuerySelector("input[name='Input.RememberMe']"));
        Assert.NotNull(document.QuerySelector("label[for='Input_RememberMe']"));
        Assert.NotNull(document.QuerySelector("button[type='submit']"));
        Assert.NotNull(document.QuerySelector("a[href='/Account/ForgotPassword']"));
        Assert.NotNull(document.QuerySelector("a[href='/Account/Register']"));
    }

    [Fact]
    public async Task Get_Login_WhenAuthenticated_RedirectsToRoot()
    {
        var gateway = new TestAccountIdentityGateway();
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await client.GetAsync("/Account/Login", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Post_Login_WithValidCredentials_ReturnsRedirect()
    {
        var gateway = new TestAccountIdentityGateway
        {
            SignInResult = PasswordSignInResult.Success
        };
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Login");

        var formData = new Dictionary<string, string>
        {
            ["Input.Email"] = "user@example.com",
            ["Input.Password"] = "SecurePassword123!",
            ["Input.RememberMe"] = "true"
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Login",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Post_Login_WithInvalidCredentials_Returns422WithValidationError()
    {
        var gateway = new TestAccountIdentityGateway
        {
            SignInResult = PasswordSignInResult.InvalidCredentials
        };
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Login");

        var formData = new Dictionary<string, string>
        {
            ["Input.Email"] = "user@example.com",
            ["Input.Password"] = "WrongPassword!",
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Login",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("E-mail ou senha incorretos", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_Login_WhenAccountDeactivated_Returns403Forbidden()
    {
        var gateway = new TestAccountIdentityGateway
        {
            SignInResult = PasswordSignInResult.Deactivated
        };
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Login");

        var formData = new Dictionary<string, string>
        {
            ["Input.Email"] = "deactivated@example.com",
            ["Input.Password"] = "Password123!",
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Login",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_Register_RendersRegistrationForm()
    {
        var gateway = new TestAccountIdentityGateway();
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (response, document, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Register");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(document.QuerySelector("input[name='Input.Email']"));
        Assert.NotNull(document.QuerySelector("input[name='Input.Password']"));
        Assert.NotNull(document.QuerySelector("input[name='Input.ConfirmPassword']"));
        Assert.NotNull(document.QuerySelector("button[type='submit']"));
    }

    [Fact]
    public async Task Post_Register_WhenFirstUserWithoutSmtp_CreatesAdminAndRedirectsToLogin()
    {
        var gateway = new TestAccountIdentityGateway
        {
            AnyUsersExistResult = false
        };
        var emailSender = new TestEmailSender { IsSmtpConfigured = false };
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Register");

        var formData = new Dictionary<string, string>
        {
            ["Input.Email"] = "admin@example.com",
            ["Input.Password"] = "AdminPassword123!",
            ["Input.ConfirmPassword"] = "AdminPassword123!"
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Register",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.OriginalString);
        var createdUser = Assert.Single(gateway.Users);
        Assert.True(createdUser.IsAdmin);
        Assert.True(createdUser.EmailConfirmed);
    }

    [Fact]
    public async Task Post_Register_WhenClosedMode_Returns403Forbidden()
    {
        var gateway = new TestAccountIdentityGateway
        {
            AnyUsersExistResult = true,
            RegistrationMode = RegistrationMode.Closed
        };
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Register");

        var formData = new Dictionary<string, string>
        {
            ["Input.Email"] = "newuser@example.com",
            ["Input.Password"] = "Password123!",
            ["Input.ConfirmPassword"] = "Password123!"
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Register",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_Register_WhenInviteOnlyWithoutInviteToken_Returns403Forbidden()
    {
        var gateway = new TestAccountIdentityGateway
        {
            AnyUsersExistResult = true,
            RegistrationMode = RegistrationMode.InviteOnly
        };
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Register");

        var formData = new Dictionary<string, string>
        {
            ["Input.Email"] = "newuser@example.com",
            ["Input.Password"] = "Password123!",
            ["Input.ConfirmPassword"] = "Password123!"
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Register",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_Register_WhenInviteOnlyWithValidToken_CreatesUserAndDisplaysConfirmationInstructions()
    {
        var gateway = new TestAccountIdentityGateway
        {
            AnyUsersExistResult = true,
            RegistrationMode = RegistrationMode.InviteOnly
        };
        var validInvite = new AccountInvitationDto(
            Guid.NewGuid(),
            "invited@example.com",
            "token-valid",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            false,
            null);
        gateway.Invitations.Add(validInvite);

        var emailSender = new TestEmailSender { IsSmtpConfigured = true };
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Register?inviteToken=token-valid");

        var formData = new Dictionary<string, string>
        {
            ["Input.Email"] = "invited@example.com",
            ["Input.Password"] = "Password123!",
            ["Input.ConfirmPassword"] = "Password123!",
            ["Input.InviteToken"] = "token-valid"
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Register",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Confirme seu e-mail", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_AcceptInvite_WithToken_RedirectsToRegister()
    {
        var gateway = new TestAccountIdentityGateway();
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await client.GetAsync("/Account/AcceptInvite?token=sample-invite-token", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Register?inviteToken=sample-invite-token", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Post_Logout_SignsOutAndRedirectsToRoot()
    {
        var gateway = new TestAccountIdentityGateway();
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/");

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Logout",
            [],
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        Assert.True(gateway.SignedOut);
    }

    [Fact]
    public async Task Get_ConfirmEmail_WithValidToken_Returns200AndRendersSuccessAlert()
    {
        var gateway = new TestAccountIdentityGateway
        {
            ConfirmEmailResult = Result.Ok()
        };
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var userId = Guid.NewGuid();
        var (response, document, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(
            client,
            $"/Account/ConfirmEmail?userId={userId}&token=valid-token");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(document.QuerySelector(".alert-success"));
        Assert.NotNull(document.QuerySelector("a[href='/Account/Login']"));
    }

    [Fact]
    public async Task Get_ConfirmEmail_WithInvalidToken_Returns422AndRendersErrorWithResendLink()
    {
        var gateway = new TestAccountIdentityGateway
        {
            ConfirmEmailResult = Result.Invalid(Message.Error("accounts.confirm_email.invalid_token"))
        };
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (response, document, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(
            client,
            "/Account/ConfirmEmail?userId=00000000-0000-0000-0000-000000000000&token=");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(document.QuerySelector(".alert-error"));
        Assert.NotNull(document.QuerySelector("a[href='/Account/ResendConfirmation']"));
    }

    [Fact]
    public async Task Post_ResendConfirmation_WhenSmtpConfigured_Returns200AndRendersSentMessage()
    {
        var testUserId = UserId.New();
        var gateway = new TestAccountIdentityGateway();
        gateway.Users.Add(new AccountUserDto(testUserId, "pending@example.com", false, true, false, DateTime.UtcNow));

        var emailSender = new TestEmailSender { IsSmtpConfigured = true };
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/ResendConfirmation");

        var formData = new Dictionary<string, string>
        {
            ["Email"] = "pending@example.com"
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/ResendConfirmation",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("um novo link foi enviado", html, StringComparison.Ordinal);
        Assert.Single(emailSender.SentMessages);
    }

    [Fact]
    public async Task Post_ForgotPassword_SendsResetEmailAndRendersSentMessage()
    {
        var testUserId = UserId.New();
        var gateway = new TestAccountIdentityGateway();
        gateway.Users.Add(new AccountUserDto(testUserId, "reset@example.com", false, true, true, DateTime.UtcNow));

        var emailSender = new TestEmailSender { IsSmtpConfigured = true };
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/ForgotPassword");

        var formData = new Dictionary<string, string>
        {
            ["Email"] = "reset@example.com"
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/ForgotPassword",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("as instruções de recuperação foram enviadas", html, StringComparison.Ordinal);
        Assert.Single(emailSender.SentMessages);
    }

    [Fact]
    public async Task Post_ResetPassword_WithMatchingPasswords_ResetsPasswordAndRendersSuccess()
    {
        var gateway = new TestAccountIdentityGateway
        {
            ResetPasswordResult = Result.Ok()
        };
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var userId = Guid.NewGuid();
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(
            client,
            $"/Account/ResetPassword?userId={userId}&token=reset-token");

        var formData = new Dictionary<string, string>
        {
            ["Input.UserId"] = userId.ToString(),
            ["Input.Token"] = "reset-token",
            ["Input.NewPassword"] = "NewPassword123!",
            ["Input.ConfirmPassword"] = "NewPassword123!"
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/ResetPassword",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Senha alterada com sucesso", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_ResetPassword_WithMismatchedPasswords_Returns422ValidationError()
    {
        var gateway = new TestAccountIdentityGateway();
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var userId = Guid.NewGuid();
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(
            client,
            $"/Account/ResetPassword?userId={userId}&token=reset-token");

        var formData = new Dictionary<string, string>
        {
            ["Input.UserId"] = userId.ToString(),
            ["Input.Token"] = "reset-token",
            ["Input.NewPassword"] = "NewPassword123!",
            ["Input.ConfirmPassword"] = "DifferentPassword123!"
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/ResetPassword",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("A confirmação de senha não confere", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_Settings_WhenUnauthenticated_Returns403Forbidden()
    {
        var gateway = new TestAccountIdentityGateway();
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await client.GetAsync("/Account/Settings", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_Settings_WhenAuthenticated_Returns200WithSettings()
    {
        var userId = UserId.New();
        var gateway = new TestAccountIdentityGateway();
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore
        {
            Settings = UserSettings.CreateDefault(userId)
        };
        var currentUser = new TestCurrentUser { IsAuthenticated = true, Id = userId };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (response, document, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(document.QuerySelector("select[name='SettingsInput.TimeZoneId']"));
        Assert.NotNull(document.QuerySelector("input[name='SettingsInput.PomodoroFocusMinutes']"));
        Assert.NotNull(document.QuerySelector("input[name='SettingsInput.PomodoroBreakMinutes']"));
        Assert.NotNull(document.QuerySelector("input[name='PasswordInput.CurrentPassword']"));
    }

    [Fact]
    public async Task Post_Settings_UpdatesUserSettingsAndRedirects()
    {
        var userId = UserId.New();
        var gateway = new TestAccountIdentityGateway();
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore
        {
            Settings = UserSettings.CreateDefault(userId)
        };
        var currentUser = new TestCurrentUser { IsAuthenticated = true, Id = userId };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Settings");

        var formData = new Dictionary<string, string>
        {
            ["SettingsInput.TimeZoneId"] = "America/New_York",
            ["SettingsInput.PomodoroFocusMinutes"] = "50",
            ["SettingsInput.PomodoroBreakMinutes"] = "10"
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Settings?handler=Settings",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Settings", response.Headers.Location?.OriginalString);
        Assert.Equal("America/New_York", settingsStore.Settings?.TimeZoneId);
        Assert.Equal(50, settingsStore.Settings?.PomodoroFocusMinutes);
        Assert.Equal(10, settingsStore.Settings?.PomodoroBreakMinutes);
    }

    [Fact]
    public async Task Post_Password_WithValidPasswords_ChangesPasswordAndRedirects()
    {
        var userId = UserId.New();
        var gateway = new TestAccountIdentityGateway
        {
            ChangePasswordResult = Result.Ok()
        };
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true, Id = userId };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Settings");

        var formData = new Dictionary<string, string>
        {
            ["PasswordInput.CurrentPassword"] = "OldPassword123!",
            ["PasswordInput.NewPassword"] = "NewPassword123!",
            ["PasswordInput.ConfirmPassword"] = "NewPassword123!"
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Account/Settings?handler=Password",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Settings", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Get_AdminUsers_WhenNonAdmin_Returns403Forbidden()
    {
        var gateway = new TestAccountIdentityGateway();
        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true, IsAdmin = false };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await client.GetAsync("/Admin/Users", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_AdminUsers_WhenAdmin_Returns200WithUsersAndInvitations()
    {
        var gateway = new TestAccountIdentityGateway();
        var adminId = UserId.New();
        gateway.Users.Add(new AccountUserDto(adminId, "admin@example.com", true, true, true, DateTime.UtcNow));

        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true, IsAdmin = true, Id = adminId };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (response, document, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Admin/Users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(document.QuerySelector("table[aria-label='Usuários']"));
        Assert.Contains("admin@example.com", document.Body!.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_AdminToggleActive_TogglesUserActiveStatus()
    {
        var gateway = new TestAccountIdentityGateway();
        var adminId = UserId.New();
        var targetId = UserId.New();
        gateway.Users.Add(new AccountUserDto(adminId, "admin@example.com", true, true, true, DateTime.UtcNow));
        gateway.Users.Add(new AccountUserDto(targetId, "target@example.com", false, true, true, DateTime.UtcNow));

        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true, IsAdmin = true, Id = adminId };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Admin/Users");

        var formData = new Dictionary<string, string>
        {
            ["userId"] = targetId.Value.ToString(),
            ["isActive"] = "false"
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Admin/Users?handler=ToggleActive",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin/Users", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Post_AdminInvite_CreatesInvitationAndDisplaysGeneratedLink()
    {
        var gateway = new TestAccountIdentityGateway();
        var adminId = UserId.New();
        gateway.Users.Add(new AccountUserDto(adminId, "admin@example.com", true, true, true, DateTime.UtcNow));

        var emailSender = new TestEmailSender { IsSmtpConfigured = false };
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true, IsAdmin = true, Id = adminId };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Admin/Users");

        var formData = new Dictionary<string, string>
        {
            ["email"] = "invitee@example.com"
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Admin/Users?handler=Invite",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Link do convite gerado", html, StringComparison.Ordinal);
        Assert.Contains("for=\"generated-invite-link\"", html, StringComparison.Ordinal);
        Assert.Single(gateway.Invitations);
    }

    [Fact]
    public async Task Post_AdminManualLink_GeneratesManualLinkAndDisplaysIt()
    {
        var gateway = new TestAccountIdentityGateway();
        var adminId = UserId.New();
        var targetId = UserId.New();
        gateway.Users.Add(new AccountUserDto(adminId, "admin@example.com", true, true, true, DateTime.UtcNow));
        gateway.Users.Add(new AccountUserDto(targetId, "unconfirmed@example.com", false, true, false, DateTime.UtcNow));

        var emailSender = new TestEmailSender();
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true, IsAdmin = true, Id = adminId };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Admin/Users");

        var formData = new Dictionary<string, string>
        {
            ["userId"] = targetId.Value.ToString(),
            ["kind"] = ManualLinkKind.EmailConfirmation.ToString()
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Admin/Users?handler=ManualLink",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Link gerado para entrega manual", html, StringComparison.Ordinal);
        Assert.Contains("for=\"generated-manual-link\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_AdminTestEmail_WhenSmtpConfigured_SendsTestEmailAndShowsSuccess()
    {
        var gateway = new TestAccountIdentityGateway();
        var adminId = UserId.New();
        gateway.Users.Add(new AccountUserDto(adminId, "admin@example.com", true, true, true, DateTime.UtcNow));

        var emailSender = new TestEmailSender { IsSmtpConfigured = true };
        var settingsStore = new TestUserSettingsStore();
        var currentUser = new TestCurrentUser { IsAuthenticated = true, IsAdmin = true, Id = adminId };

        using var factory = AccountTestHelper.CreateTestFactory(baseFactory, gateway, emailSender, settingsStore, currentUser);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true, HandleCookies = true });

        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Admin/Users");

        var formData = new Dictionary<string, string>
        {
            ["recipientEmail"] = "testrecipient@example.com"
        };

        using var response = await AccountTestHelper.PostFormWithAntiforgeryAsync(
            client,
            "/Admin/Users?handler=TestEmail",
            formData,
            csrfHeader,
            csrfToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("E-mail de teste enviado com sucesso", html, StringComparison.Ordinal);
        Assert.Single(emailSender.SentMessages);
    }
}
