using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Trail.E2E.Tests;

[Collection(E2ETestCollectionDefinition.Name)]
public sealed partial class AccountsE2ETests(DockerAppFixture fixture)
{
    [Fact]
    public async Task Accounts_FullLifecycleAndAccessibilityScans_PassWithZeroViolations()
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
        await main.GetByLabel(UiText.Get("Email"), new() { Exact = true }).FillAsync("second@trail.local");
        await main.GetByLabel(UiText.Get("Password"), new() { Exact = true }).FillAsync("SecondPass123!");
        await main.GetByLabel(UiText.Get("ConfirmPassword"), new() { Exact = true }).FillAsync("SecondPass123!");

        var closedRegistration = await page.RunAndWaitForResponseAsync(
            () => main.GetByRole(AriaRole.Button, new() { Name = UiText.Get("Register"), Exact = true }).ClickAsync(),
            response => response.Request.Method == "POST" && response.Url.Contains("/Account/Register", StringComparison.Ordinal));

        Assert.Equal(403, closedRegistration.Status);
        await Expect(main).ToContainTextAsync(UiText.Get("accounts.registration.closed"));

        await page.GotoAsync(fixture.BaseUrl + "/Account/Login");
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(UiText.Get("LoginHeading"));

        await main.GetByLabel(UiText.Get("Email"), new() { Exact = true }).FillAsync(DockerAppFixture.AdminEmail);
        await main.GetByLabel(UiText.Get("Password"), new() { Exact = true }).FillAsync(DockerAppFixture.AdminPassword);
        await main.GetByLabel(UiText.Get("RememberMe"), new() { Exact = true }).CheckAsync();
        await main.GetByRole(AriaRole.Button, new() { Name = UiText.Get("Login"), Exact = true }).ClickAsync();

        await Expect(page).Not.ToHaveURLAsync(LoginPathPattern());
        var banner = page.GetByRole(AriaRole.Banner);
        await Expect(banner.GetByRole(AriaRole.Link, new() { Name = UiText.Get("Settings"), Exact = true })).ToBeVisibleAsync();
        await Expect(banner.GetByRole(AriaRole.Link, new() { Name = UiText.Get("Administration"), Exact = true })).ToBeVisibleAsync();

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

        await page.GotoAsync(fixture.BaseUrl + "/Admin/Users");
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToContainTextAsync(UiText.Get("AdministrationHeading"));

        await AxeTestHelper.AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);

        var registrationMode = main.GetByRole(AriaRole.Region, new() { Name = UiText.Get("RegistrationClosedHeading"), Exact = true });
        await Expect(registrationMode).ToContainTextAsync(UiText.Get("RegistrationClosedHowToInvite"));
        await Expect(page.Locator("#invite-email")).ToHaveCountAsync(0);
    }

    [GeneratedRegex("/Account/Login(\\?.*)?$")]
    private static partial Regex LoginPathPattern();
}
