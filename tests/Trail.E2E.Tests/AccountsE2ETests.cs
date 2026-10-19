using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Trail.E2E.Tests;

[Collection(E2ETestCollectionDefinition.Name)]
public sealed partial class AccountsE2ETests(DockerAppFixture fixture)
{
    private const string Password = "InvitedPass123!";

    [Fact]
    public async Task Register_WithoutInvitation_IsRefusedAndPublicPagesHaveZeroViolations()
    {
        var page = await fixture.CreatePageAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
        });

        var unauthenticatedRoutes = new[]
        {
            "/Account/Login",
            "/Account/Register",
            "/Account/ForgotPassword"
        };

        foreach (var route in unauthenticatedRoutes)
        {
            await page.GotoAsync(fixture.BaseUrl + route);
            await AxeTestHelper.AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);
        }

        await page.GotoAsync(fixture.BaseUrl + "/Account/Register");
        var main = page.GetByRole(AriaRole.Main);
        await main.GetByLabel(UiText.Get("Email"), new() { Exact = true }).FillAsync($"uninvited-{Guid.NewGuid():N}@trail.local");
        await main.GetByLabel(UiText.Get("Password"), new() { Exact = true }).FillAsync(Password);
        await main.GetByLabel(UiText.Get("ConfirmPassword"), new() { Exact = true }).FillAsync(Password);

        var refusal = await page.RunAndWaitForResponseAsync(
            () => main.GetByRole(AriaRole.Button, new() { Name = UiText.Get("Register"), Exact = true }).ClickAsync(),
            response => response.Request.Method == "POST" && response.Url.Contains("/Account/Register", StringComparison.Ordinal));

        Assert.Equal(403, refusal.Status);
        await Expect(main).ToContainTextAsync(UiText.Get("accounts.registration.invite_required"));
    }

    [Fact]
    public async Task Invitation_RegisterConfirmSignInAndChangeSettings_WorksWithZeroAccessibilityViolations()
    {
        var email = $"invited-{Guid.NewGuid():N}@trail.local";
        var admin = await fixture.CreateAdminPageAsync(1280, 800);
        var adminMain = admin.GetByRole(AriaRole.Main);

        await admin.GotoAsync(fixture.BaseUrl + "/Admin/Users");
        await Expect(adminMain.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToContainTextAsync(UiText.Get("AdministrationHeading"));

        var inviteRegion = adminMain.GetByRole(AriaRole.Region, new() { Name = UiText.Get("InviteUser"), Exact = true });
        await inviteRegion.GetByLabel(UiText.Get("Email"), new() { Exact = true }).FillAsync(email);
        await inviteRegion.GetByRole(AriaRole.Button, new() { Name = UiText.Get("SendInvitation"), Exact = true }).ClickAsync();

        await Expect(adminMain.GetByRole(AriaRole.Status).Filter(new() { HasText = UiText.Get("InvitationCreated") })).ToBeVisibleAsync();
        var invitationLink = await adminMain.GetByLabel(UiText.Get("InvitationLinkGenerated"), new() { Exact = true }).InputValueAsync();
        await Expect(adminMain.GetByRole(AriaRole.Table, new() { Name = UiText.Get("Invitations"), Exact = true })).ToContainTextAsync(email);
        await AxeTestHelper.AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(admin);

        var page = await fixture.CreatePageAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
        });
        var main = page.GetByRole(AriaRole.Main);

        await page.GotoAsync(fixture.BaseUrl + new Uri(invitationLink).PathAndQuery);
        await main.GetByLabel(UiText.Get("Email"), new() { Exact = true }).FillAsync(email);
        await main.GetByLabel(UiText.Get("Password"), new() { Exact = true }).FillAsync(Password);
        await main.GetByLabel(UiText.Get("ConfirmPassword"), new() { Exact = true }).FillAsync(Password);

        var registration = await page.RunAndWaitForResponseAsync(
            () => main.GetByRole(AriaRole.Button, new() { Name = UiText.Get("Register"), Exact = true }).ClickAsync(),
            response => response.Request.Method == "POST" && response.Url.Contains("/Account/Register", StringComparison.Ordinal));

        Assert.Equal(200, registration.Status);
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(UiText.Get("RegistrationConfirmationSentTitle"));

        await admin.GotoAsync(fixture.BaseUrl + "/Admin/Users");
        var invitedRow = adminMain.GetByRole(AriaRole.Row).Filter(new() { HasText = email });
        await invitedRow.GetByRole(AriaRole.Button, new() { Name = UiText.Get("GenerateConfirmationLink"), Exact = true }).ClickAsync();
        var confirmationLink = await adminMain.GetByLabel(UiText.Get("ManualLinkGenerated"), new() { Exact = true }).InputValueAsync();

        await page.GotoAsync(fixture.BaseUrl + new Uri(confirmationLink).PathAndQuery);
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(UiText.Get("ConfirmEmailSuccessTitle"));
        await main.GetByRole(AriaRole.Link, new() { Name = UiText.Get("GoToLogin"), Exact = true }).ClickAsync();

        await Expect(page).ToHaveURLAsync(LoginPathPattern());
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(UiText.Get("LoginHeading"));
        await main.GetByLabel(UiText.Get("Email"), new() { Exact = true }).FillAsync(email);
        await main.GetByLabel(UiText.Get("Password"), new() { Exact = true }).FillAsync(Password);
        await main.GetByLabel(UiText.Get("RememberMe"), new() { Exact = true }).CheckAsync();
        await main.GetByRole(AriaRole.Button, new() { Name = UiText.Get("Login"), Exact = true }).ClickAsync();

        await Expect(page).Not.ToHaveURLAsync(LoginPathPattern());
        var banner = page.GetByRole(AriaRole.Banner);
        await Expect(banner.GetByRole(AriaRole.Link, new() { Name = UiText.Get("Settings"), Exact = true })).ToBeVisibleAsync();
        await Expect(banner.GetByRole(AriaRole.Link, new() { Name = UiText.Get("Administration"), Exact = true })).ToHaveCountAsync(0);

        await page.GotoAsync(fixture.BaseUrl + "/Account/Settings");
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToContainTextAsync(UiText.Get("SettingsTitle"));

        await AxeTestHelper.AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);

        await page.SelectOptionAsync("select[name='SettingsInput.TimeZoneId']", "Europe/Lisbon");
        await page.FillAsync("input[name='SettingsInput.PomodoroFocusMinutes']", "40");
        await page.FillAsync("input[name='SettingsInput.PomodoroBreakMinutes']", "8");

        var settingsResponse = await page.RunAndWaitForResponseAsync(
            () => page.ClickAsync("#settings-form-container button[type='submit']"),
            response => response.Request.Method == "POST" && response.Url.Contains("/Account/Settings", StringComparison.Ordinal));
        Assert.Equal(200, settingsResponse.Status);

        await page.ReloadAsync();
        await Expect(page.Locator("select[name='SettingsInput.TimeZoneId']")).ToHaveValueAsync("Europe/Lisbon");
        await Expect(page.Locator("input[name='SettingsInput.PomodoroFocusMinutes']")).ToHaveValueAsync("40");
        await Expect(page.Locator("input[name='SettingsInput.PomodoroBreakMinutes']")).ToHaveValueAsync("8");
    }

    [GeneratedRegex("/Account/Login(\\?.*)?$")]
    private static partial Regex LoginPathPattern();
}
