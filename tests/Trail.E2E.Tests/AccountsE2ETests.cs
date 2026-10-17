using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Trail.E2E.Tests;

public sealed class AccountsE2ETests(DockerAppFixture fixture) : IClassFixture<DockerAppFixture>
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
            await AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);
        }

        await page.SetViewportSizeAsync(1280, 800);
        await page.GotoAsync(fixture.BaseUrl + "/Account/Register");

        await page.FillAsync("input[name='Input.Email']", "admin@trail.local");
        await page.FillAsync("input[name='Input.Password']", "AdminPass123!");
        await page.FillAsync("input[name='Input.ConfirmPassword']", "AdminPass123!");
        await page.ClickAsync("button[type='submit']");

        await page.WaitForURLAsync(url => url.Contains("/Account/Login", StringComparison.Ordinal));
        await Expect(page.Locator("h1")).ToContainTextAsync("Acesse sua conta");

        await page.FillAsync("input[name='Input.Email']", "admin@trail.local");
        await page.FillAsync("input[name='Input.Password']", "AdminPass123!");
        await page.CheckAsync("input[name='Input.RememberMe']");
        await page.ClickAsync("button[type='submit']");

        await page.WaitForURLAsync(url => !url.Contains("/Account/Login", StringComparison.Ordinal));
        await Expect(page.Locator("header a[href='/Account/Settings']")).ToBeVisibleAsync();
        await Expect(page.Locator("header a[href='/Admin/Users']")).ToBeVisibleAsync();

        await page.GotoAsync(fixture.BaseUrl + "/Account/Settings");
        await Expect(page.Locator("h1")).ToContainTextAsync("Preferências");

        await AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);

        await page.SetViewportSizeAsync(1280, 800);
        await page.SelectOptionAsync("select[name='SettingsInput.TimeZoneId']", "Europe/Lisbon");
        await page.FillAsync("input[name='SettingsInput.PomodoroFocusMinutes']", "40");
        await page.FillAsync("input[name='SettingsInput.PomodoroBreakMinutes']", "8");

        var responseTask = page.WaitForResponseAsync(resp => resp.Url.Contains("/Account/Settings", StringComparison.Ordinal) && resp.Status == 200);
        await page.ClickAsync("#settings-form-container button[type='submit']");
        await responseTask;

        await page.ReloadAsync();
        await Expect(page.Locator("select[name='SettingsInput.TimeZoneId']")).ToHaveValueAsync("Europe/Lisbon");
        await Expect(page.Locator("input[name='SettingsInput.PomodoroFocusMinutes']")).ToHaveValueAsync("40");
        await Expect(page.Locator("input[name='SettingsInput.PomodoroBreakMinutes']")).ToHaveValueAsync("8");

        await page.GotoAsync(fixture.BaseUrl + "/Admin/Users");
        await Expect(page.Locator("h1")).ToContainTextAsync("Gestão de Usuários");

        await AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);

        await page.SetViewportSizeAsync(1280, 800);
        await page.FillAsync("input#invite-email", "invited@trail.local");
        await page.ClickAsync("section[aria-labelledby='invite-heading'] button[type='submit']");

        var generatedInviteInput = page.Locator("#generated-invite-link");
        await Expect(generatedInviteInput).ToBeVisibleAsync();
        var inviteLink = await generatedInviteInput.InputValueAsync();
        Assert.False(string.IsNullOrWhiteSpace(inviteLink));
        Assert.Contains("inviteToken=", inviteLink, StringComparison.Ordinal);

        await AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);
    }

    private static async Task AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(IPage page)
    {
        await page.SetViewportSizeAsync(1280, 800);
        await page.EvaluateAsync("() => { document.documentElement.setAttribute('data-theme', 'trail-light'); localStorage.setItem('trail-theme', 'trail-light'); }");
        await page.WaitForTimeoutAsync(250);
        var desktopLight = await page.RunAxe();
        Assert.Empty(desktopLight.Violations);

        await page.ClickAsync("[data-theme-toggle]");
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "trail-dark");
        await page.WaitForTimeoutAsync(250);
        var desktopDark = await page.RunAxe();
        Assert.Empty(desktopDark.Violations);

        await page.SetViewportSizeAsync(390, 844);
        await page.ClickAsync("[data-theme-toggle]");
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "trail-light");
        await page.WaitForTimeoutAsync(250);
        var mobileLight = await page.RunAxe();
        Assert.Empty(mobileLight.Violations);

        await page.ClickAsync("[data-theme-toggle]");
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "trail-dark");
        await page.WaitForTimeoutAsync(250);
        var mobileDark = await page.RunAxe();
        Assert.Empty(mobileDark.Violations);

        await page.ClickAsync("[data-theme-toggle]");
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "trail-light");
        await page.WaitForTimeoutAsync(250);
        await page.SetViewportSizeAsync(1280, 800);
    }
}
