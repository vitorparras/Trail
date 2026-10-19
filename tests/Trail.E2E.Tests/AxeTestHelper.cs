using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Trail.E2E.Tests;

public static class AxeTestHelper
{
    private const string LightTheme = "trail-light";
    private const string DarkTheme = "trail-dark";

    private const string WaitForStableRenderingScript = """
        async () => {
            await document.fonts.ready;
            await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
            const finiteAnimations = document.getAnimations()
                .filter(animation => animation.effect
                    && animation.timeline === document.timeline
                    && animation.effect.getComputedTiming().iterations !== Infinity);
            const pending = new Set(finiteAnimations);
            const describe = animation => {
                const target = animation.effect.target;
                const name = animation.animationName || animation.transitionProperty || animation.constructor.name;
                const element = target ? target.tagName.toLowerCase() + (target.id ? '#' + target.id : '') : 'unknown';
                return `${name} (${animation.playState}) on ${element}${animation.effect.pseudoElement ?? ''}`;
            };
            const settled = Promise.all(finiteAnimations.map(animation => animation.finished
                .catch(() => undefined)
                .then(() => pending.delete(animation)))).then(() => []);
            const capped = new Promise(resolve => setTimeout(() => resolve([...pending].map(describe)), 3000));
            return await Promise.race([settled, capped]);
        }
        """;

    private static readonly TimeSpan AxeRunTimeout = TimeSpan.FromSeconds(60);

    public static async Task AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var originalViewport = page.ViewportSize ?? new PageViewportSizeResult { Width = 1280, Height = 800 };

        await page.EvaluateAsync($"() => {{ document.documentElement.setAttribute('data-theme', '{LightTheme}'); localStorage.setItem('trail-theme', '{LightTheme}'); }}");

        await page.SetViewportSizeAsync(1280, 800);
        await AssertNoViolationsAsync(page, LightTheme, "desktop");

        await ToggleThemeAsync(page, DarkTheme);
        await AssertNoViolationsAsync(page, DarkTheme, "desktop");

        await page.SetViewportSizeAsync(390, 844);
        await AssertNoViolationsAsync(page, DarkTheme, "mobile");

        await ToggleThemeAsync(page, LightTheme);
        await AssertNoViolationsAsync(page, LightTheme, "mobile");

        await page.SetViewportSizeAsync(originalViewport.Width, originalViewport.Height);
        await WaitForStableRenderingAsync(page);
    }

    public static async Task AssertAxeZeroViolationsInBothThemesAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var originalTheme = await page.EvaluateAsync<string?>("() => document.documentElement.getAttribute('data-theme')");
        var viewport = page.ViewportSize is { } size ? $"{size.Width}x{size.Height}" : "default";

        foreach (var theme in new[] { LightTheme, DarkTheme })
        {
            await page.EvaluateAsync("theme => document.documentElement.setAttribute('data-theme', theme)", theme);
            await AssertNoViolationsAsync(page, theme, viewport);
        }

        await page.EvaluateAsync("theme => document.documentElement.setAttribute('data-theme', theme)", originalTheme ?? LightTheme);
        await WaitForStableRenderingAsync(page);
    }

    private static async Task ToggleThemeAsync(IPage page, string expectedTheme)
    {
        await page.ClickAsync("[data-theme-toggle]");
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", expectedTheme);
    }

    private static async Task AssertNoViolationsAsync(IPage page, string theme, string viewport)
    {
        await WaitForStableRenderingAsync(page);

        var result = await page.RunAxe().WaitAsync(AxeRunTimeout, TestContext.Current.CancellationToken);
        Assert.True(
            result.Violations.Length == 0,
            $"Accessibility violations on {page.Url} ({theme}, {viewport}): "
                + string.Join("; ", result.Violations.Select(v => $"{v.Id}: {string.Join(", ", v.Nodes.Select(n => n.Html))}")));
    }

    private static async Task WaitForStableRenderingAsync(IPage page)
    {
        var pending = await page.EvaluateAsync<string[]>(WaitForStableRenderingScript);
        if (pending.Length > 0)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Animations still pending on {page.Url} after the stable rendering wait: {string.Join("; ", pending)}");
        }
    }
}
