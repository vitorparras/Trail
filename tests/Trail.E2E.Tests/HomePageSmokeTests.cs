using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Trail.E2E.Tests;

[Collection(E2ETestCollectionDefinition.Name)]
public sealed class HomePageSmokeTests(DockerAppFixture fixture)
{
    [Fact]
    public async Task Desktop_LightTheme_RendersShellAndPassesAccessibilityScan()
    {
        var page = await fixture.CreatePageAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
        });

        var response = await page.GotoAsync(fixture.BaseUrl + "/");
        Assert.NotNull(response);
        Assert.True(response.Ok);
        Assert.Equal(200, response.Status);

        await Expect(page.Locator("html")).ToHaveAttributeAsync("lang", "pt-BR");
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "trail-light");

        await Expect(page.Locator(".lg\\:block nav[aria-label]")).ToBeVisibleAsync();
        await Expect(page.Locator("[data-dialog-open='mobile-nav-dialog']")).ToBeHiddenAsync();
        await Expect(page.Locator("main#main-content")).ToBeVisibleAsync();
        await Expect(page.Locator("footer a[data-footer-source]")).ToBeVisibleAsync();

        var axeResult = await page.RunAxe();
        Assert.Empty(axeResult.Violations);
    }

    [Fact]
    public async Task Desktop_DarkTheme_SwitchesAndPersistsAcrossReloadWithoutAccessibilityViolations()
    {
        var page = await fixture.CreatePageAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
        });

        await page.GotoAsync(fixture.BaseUrl + "/");

        await page.ClickAsync("[data-theme-toggle]");
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "trail-dark");

        var storedTheme = await page.EvaluateAsync<string>("() => localStorage.getItem('trail-theme')");
        Assert.Equal("trail-dark", storedTheme);

        await page.ReloadAsync();
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "trail-dark");

        var storedThemeAfterReload = await page.EvaluateAsync<string>("() => localStorage.getItem('trail-theme')");
        Assert.Equal("trail-dark", storedThemeAfterReload);

        var axeResult = await page.RunAxe();
        Assert.Empty(axeResult.Violations);
    }

    [Fact]
    public async Task Mobile_LightTheme_RendersAndInteractsWithNavigationDialogWithoutAccessibilityViolations()
    {
        var page = await fixture.CreatePageAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 390, Height = 844 }
        });

        await page.GotoAsync(fixture.BaseUrl + "/");

        var mobileTrigger = page.Locator("[data-dialog-open='mobile-nav-dialog']");
        await Expect(mobileTrigger).ToBeVisibleAsync();

        var initialAxe = await page.RunAxe();
        Assert.Empty(initialAxe.Violations);

        await mobileTrigger.ClickAsync();
        var dialog = page.Locator("#mobile-nav-dialog");
        await Expect(dialog).ToBeVisibleAsync();

        var openDialogAxe = await page.RunAxe();
        Assert.Empty(openDialogAxe.Violations);

        await page.ClickAsync("#mobile-nav-dialog [data-dialog-close]");
        await Expect(dialog).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Mobile_DarkTheme_SwitchesAndInteractsWithNavigationDialogWithoutAccessibilityViolations()
    {
        var page = await fixture.CreatePageAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 390, Height = 844 }
        });

        await page.GotoAsync(fixture.BaseUrl + "/");

        await page.ClickAsync("[data-theme-toggle]");
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "trail-dark");

        var initialDarkAxe = await page.RunAxe();
        Assert.Empty(initialDarkAxe.Violations);

        var mobileTrigger = page.Locator("[data-dialog-open='mobile-nav-dialog']");
        await mobileTrigger.ClickAsync();
        var dialog = page.Locator("#mobile-nav-dialog");
        await Expect(dialog).ToBeVisibleAsync();

        var openDialogDarkAxe = await page.RunAxe();
        Assert.Empty(openDialogDarkAxe.Violations);

        await page.ClickAsync("#mobile-nav-dialog [data-dialog-close]");
        await Expect(dialog).ToBeHiddenAsync();
    }
}
