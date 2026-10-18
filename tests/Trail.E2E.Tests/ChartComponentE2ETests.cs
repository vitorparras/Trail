using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Trail.E2E.Tests;

[Collection(E2ETestCollectionDefinition.Name)]
public sealed class ChartComponentE2ETests(DockerAppFixture fixture)
{
    private const double MinimumSeriesContrast = 3;

    private const double MinimumHoverContrast = 1.5;

    private const string ChartExists = "(selector) => !!Chart.getChart(document.querySelector(selector))";

    private const string InitialChartsCreated = "() => Object.keys(Chart.instances).length >= 2";

    private const string InstanceCountIs = "(expected) => Object.keys(Chart.instances).length === expected";

    private const string IsDetached = "(element) => !element.isConnected";

    private const string HasDrawnPixels = """
        (selector) => {
            const canvas = document.querySelector(selector);
            if (!canvas || !Chart.getChart(canvas)) {
                return false;
            }
            const data = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height).data;
            for (let index = 3; index < data.length; index += 4) {
                if (data[index] !== 0) {
                    return true;
                }
            }
            return false;
        }
        """;

    private const string IsMorphedAndLaidOut = """
        (selector) => {
            const canvas = document.querySelector(selector);
            if (!canvas || canvas !== window.morphProbe || !Chart.getChart(canvas)) {
                return false;
            }
            const frame = canvas.closest('.chart-frame');
            return Math.abs(canvas.getBoundingClientRect().width - frame.getBoundingClientRect().width) <= 1
                && frame.style.height !== ''
                && getComputedStyle(canvas).display === 'block';
        }
        """;

    private const string DatasetColors = """
        (selector) => Chart.getChart(document.querySelector(selector)).data.datasets.map((dataset) => ({
            background: dataset.backgroundColor,
            border: dataset.borderColor,
            hoverBackground: dataset.hoverBackgroundColor,
            hoverBorder: dataset.hoverBorderColor
        }))
        """;

    private const string PaletteContrasts = """
        (selector) => {
            const probe = document.createElement('canvas');
            probe.width = 1;
            probe.height = 1;
            const context = probe.getContext('2d', { willReadFrequently: true });
            const rgb = (value) => {
                context.clearRect(0, 0, 1, 1);
                context.fillStyle = 'rgba(0, 0, 0, 0)';
                context.fillStyle = value;
                context.fillRect(0, 0, 1, 1);
                return [...context.getImageData(0, 0, 1, 1).data].slice(0, 3);
            };
            const luminance = (color) => {
                const [red, green, blue] = color.map((channel) => {
                    const value = channel / 255;
                    return value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
                });
                return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
            };
            const ratio = (first, second) => {
                const [lighter, darker] = [luminance(first), luminance(second)].sort((left, right) => right - left);
                return (lighter + 0.05) / (darker + 0.05);
            };
            const styles = getComputedStyle(document.documentElement);
            const surfaces = ['--color-base-100', '--color-base-200'].map((name) => rgb(styles.getPropertyValue(name).trim()));
            return Chart.getChart(document.querySelector(selector)).data.datasets.map((dataset) => {
                const base = rgb(dataset.borderColor);
                return {
                    label: dataset.label,
                    surface: Math.min(...surfaces.map((surface) => ratio(base, surface))),
                    hover: ratio(base, rgb(dataset.hoverBorderColor))
                };
            });
        }
        """;

    private const string RenderedBarColorMatches = """
        ([selector, expected]) => {
            const chart = Chart.getChart(document.querySelector(selector));
            return chart.getDatasetMeta(0).data[0].options.backgroundColor === expected;
        }
        """;

    private const string FirstValue = "(selector) => Chart.getChart(document.querySelector(selector)).data.datasets[0].data[0]";

    private const string WaitForStableRendering = """
        async () => {
            await document.fonts.ready;
            await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
            const finiteAnimations = document.getAnimations()
                .filter(animation => animation.effect && animation.effect.getComputedTiming().iterations !== Infinity);
            await Promise.all(finiteAnimations.map(animation => animation.finished.catch(() => undefined)));
        }
        """;

    private const string HasUsablePlotHeight = "(selector) => Chart.getChart(document.querySelector(selector)).chartArea.height >= 150";

    private const string HasNoHorizontalPageOverflow = "() => document.documentElement.scrollWidth === document.documentElement.clientWidth";

    private const string RegionScrolls = "(selector) => { const region = document.querySelector(selector); return region.scrollWidth > region.clientWidth; }";

    [Theory]
    [InlineData(1280, 800)]
    [InlineData(390, 844)]
    public async Task Chart_AfterHtmxSwaps_IsDrawnAndThePreviousChartIsDestroyed(int width, int height)
    {
        var (page, consoleErrors) = await OpenFixtureAsync(width, height);

        await Expect(page.Locator("#chart-bars canvas")).ToBeVisibleAsync();
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-bars canvas");
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-line canvas");
        Assert.Equal(60, await page.EvaluateAsync<int>(FirstValue, "#chart-bars canvas"));

        var previousBars = await page.QuerySelectorAsync("#chart-bars canvas");
        await page.ClickAsync("#reload");
        await page.WaitForFunctionAsync(IsDetached, previousBars);
        await Expect(page.Locator("#region h2")).ToHaveTextAsync("Region 2");
        await page.WaitForFunctionAsync("(selector) => Chart.getChart(document.querySelector(selector))?.data.datasets[0].data[0] === 70", "#chart-bars canvas");
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-bars canvas");
        await page.WaitForFunctionAsync(InstanceCountIs, 2);

        await page.ClickAsync("#load-slot");
        await page.WaitForFunctionAsync(ChartExists, "#chart-slot canvas");
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-slot canvas");
        await page.WaitForFunctionAsync(InstanceCountIs, 3);

        var previousSlot = await page.QuerySelectorAsync("#chart-slot canvas");
        await page.ClickAsync("#load-slot");
        await page.WaitForFunctionAsync(IsDetached, previousSlot);
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-slot canvas");
        await page.WaitForFunctionAsync(InstanceCountIs, 3);

        previousBars = await page.QuerySelectorAsync("#chart-bars canvas");
        await page.ClickAsync("#reload");
        await page.WaitForFunctionAsync(IsDetached, previousBars);
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-bars canvas");
        await page.WaitForFunctionAsync(InstanceCountIs, 3);

        Assert.Empty(consoleErrors);
    }

    [Theory]
    [InlineData(1280, 800)]
    [InlineData(390, 844)]
    public async Task Chart_AfterAMorphSwapWithTheSameData_IsLaidOutAndDrawnAgain(int width, int height)
    {
        var (page, consoleErrors) = await OpenFixtureAsync(width, height);
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-bars canvas");
        await page.EvaluateAsync("() => { window.morphProbe = document.querySelector('#chart-bars canvas'); }");

        await page.ClickAsync("#morph");
        await Expect(page.Locator("#region h2")).ToHaveTextAsync("Region 3");

        await page.WaitForFunctionAsync(IsMorphedAndLaidOut, "#chart-bars canvas");
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-bars canvas");
        await page.WaitForFunctionAsync(InstanceCountIs, 2);

        Assert.Empty(consoleErrors);
    }

    [Theory]
    [InlineData(1280, 800)]
    [InlineData(390, 844)]
    public async Task Chart_WhenTheThemeChanges_IsRecoloredWithValidHoverColors(int width, int height)
    {
        var (page, consoleErrors) = await OpenFixtureAsync(width, height);
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-line canvas");

        var light = await page.EvaluateAsync<ChartColors[]>(DatasetColors, "#chart-line canvas");
        AssertValidColors(light);

        await page.ClickAsync("[data-theme-toggle]");
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "trail-dark");
        await page.WaitForFunctionAsync(
            "([selector, previous]) => Chart.getChart(document.querySelector(selector)).data.datasets[0].borderColor !== previous",
            new object[] { "#chart-line canvas", light[0].Border });

        var dark = await page.EvaluateAsync<ChartColors[]>(DatasetColors, "#chart-line canvas");
        AssertValidColors(dark);
        Assert.NotEqual(light[0].Border, dark[0].Border);
        Assert.NotEqual(light[0].HoverBorder, dark[0].HoverBorder);
        Assert.NotEqual(light[1].Border, light[0].Border);

        var barColors = await page.EvaluateAsync<ChartColors[]>(DatasetColors, "#chart-bars canvas");
        await page.WaitForFunctionAsync(RenderedBarColorMatches, new object[] { "#chart-bars canvas", barColors[0].Background });

        await page.ClickAsync("[data-theme-toggle]");
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "trail-light");
        var lightBarColors = await page.EvaluateAsync<ChartColors[]>(DatasetColors, "#chart-bars canvas");
        Assert.NotEqual(barColors[0].Background, lightBarColors[0].Background);
        await page.WaitForFunctionAsync(RenderedBarColorMatches, new object[] { "#chart-bars canvas", lightBarColors[0].Background });

        Assert.Empty(consoleErrors);
    }

    [Fact]
    public async Task Chart_Palette_HasEnoughContrastAgainstTheSurfacesAndOnHoverInBothThemes()
    {
        var (page, consoleErrors) = await OpenFixtureAsync(1280, 800);
        await page.ClickAsync("#wide summary");
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-wide canvas");

        var light = await page.EvaluateAsync<PaletteContrast[]>(PaletteContrasts, "#chart-wide canvas");
        AssertContrast(light, "light");

        var lightBorder = (await page.EvaluateAsync<ChartColors[]>(DatasetColors, "#chart-wide canvas"))[0].Border;
        await page.ClickAsync("[data-theme-toggle]");
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "trail-dark");
        await page.WaitForFunctionAsync(
            "([selector, previous]) => Chart.getChart(document.querySelector(selector)).data.datasets[0].borderColor !== previous",
            new object[] { "#chart-wide canvas", lightBorder });

        var dark = await page.EvaluateAsync<PaletteContrast[]>(PaletteContrasts, "#chart-wide canvas");
        AssertContrast(dark, "dark");

        Assert.Empty(consoleErrors);
    }

    [Theory]
    [InlineData(1280, 800)]
    [InlineData(390, 844)]
    public async Task Chart_InsideAClosedDetails_IsCreatedOnlyWhenItOpens(int width, int height)
    {
        var (page, consoleErrors) = await OpenFixtureAsync(width, height);
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-bars canvas");

        Assert.False(await page.EvaluateAsync<bool>(ChartExists, "#chart-lazy canvas"));
        await page.WaitForFunctionAsync(InstanceCountIs, 2);

        await page.ClickAsync("#lazy summary");
        await Expect(page.Locator("#chart-lazy canvas")).ToBeVisibleAsync();
        await page.WaitForFunctionAsync(ChartExists, "#chart-lazy canvas");
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-lazy canvas");
        await page.WaitForFunctionAsync(InstanceCountIs, 3);

        Assert.Empty(consoleErrors);
    }

    [Theory]
    [InlineData(390, 844)]
    [InlineData(320, 568)]
    public async Task Chart_WithAWideTable_ScrollsTheTableInsteadOfThePageAndHasNoAccessibilityViolations(int width, int height)
    {
        var (page, consoleErrors) = await OpenFixtureAsync(width, height);
        await page.ClickAsync("#wide summary");
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-wide canvas");
        await page.WaitForFunctionAsync(HasUsablePlotHeight, "#chart-wide canvas");

        foreach (var theme in new[] { "trail-light", "trail-dark" })
        {
            if (theme == "trail-dark")
            {
                await page.ClickAsync("[data-theme-toggle]");
            }

            await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", theme);
            Assert.True(await page.EvaluateAsync<bool>(HasNoHorizontalPageOverflow), $"The page overflows horizontally at {width}px ({theme}).");
            Assert.True(await page.EvaluateAsync<bool>(RegionScrolls, "#chart-wide [role='group']"), $"The wide table does not scroll inside its region at {width}px.");

            await page.EvaluateAsync(WaitForStableRendering);
            var result = await page.RunAxe();
            Assert.True(
                result.Violations.Length == 0,
                $"Accessibility violations at {width}px ({theme}): "
                    + string.Join("; ", result.Violations.Select(violation => $"{violation.Id}: {string.Join(", ", violation.Nodes.Select(node => node.Html))}")));
        }

        Assert.Empty(consoleErrors);
    }

    [Fact]
    public async Task Chart_Tooltip_FormatsNumbersWithThePageLocale()
    {
        var (page, consoleErrors) = await OpenFixtureAsync(1280, 800);
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-line canvas");

        var tooltip = await page.EvaluateAsync<string>(
            "(selector) => Chart.getChart(document.querySelector(selector)).options.plugins.tooltip.callbacks.label({ dataset: { label: 'Score' }, parsed: { y: 1234.56 } })",
            "#chart-line canvas");

        Assert.Equal("Score: 1.234,6%", tooltip);
        Assert.Empty(consoleErrors);
    }

    [Fact]
    public async Task Chart_Fixture_HasNoAccessibilityViolationsInBothThemesAndViewports()
    {
        var (page, consoleErrors) = await OpenFixtureAsync(1280, 800);
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-bars canvas");
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-line canvas");
        await page.ClickAsync("#lazy summary");
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-lazy canvas");
        await page.ClickAsync("#twins summary");
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-twin-a canvas");
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-twin-b canvas");
        await page.ClickAsync("#wide summary");
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-wide canvas");
        await page.ClickAsync("#load-slot");
        await page.WaitForFunctionAsync(HasDrawnPixels, "#chart-slot canvas");

        await AxeTestHelper.AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);

        Assert.Empty(consoleErrors);
    }

    private static void AssertContrast(PaletteContrast[] palette, string theme)
    {
        Assert.Equal(6, palette.Length);
        foreach (var entry in palette)
        {
            Assert.True(entry.Surface >= MinimumSeriesContrast, $"{entry.Label} has {entry.Surface:0.00}:1 against the surfaces in the {theme} theme.");
            Assert.True(entry.Hover >= MinimumHoverContrast, $"{entry.Label} hover has {entry.Hover:0.00}:1 against its base color in the {theme} theme.");
        }
    }

    private static void AssertValidColors(ChartColors[] datasets)
    {
        Assert.NotEmpty(datasets);
        foreach (var dataset in datasets)
        {
            foreach (var color in new[] { dataset.Background, dataset.Border, dataset.HoverBackground, dataset.HoverBorder })
            {
                Assert.Matches(@"^rgba?\(\d{1,3}, \d{1,3}, \d{1,3}(, (0|1|0?\.\d+))?\)$", color);
                Assert.DoesNotContain("90, 90, 90", color, StringComparison.Ordinal);
            }

            Assert.NotEqual(dataset.Border, dataset.HoverBorder);
        }
    }

    private async Task<(IPage Page, List<string> ConsoleErrors)> OpenFixtureAsync(int width, int height)
    {
        var page = await fixture.CreatePageAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = width, Height = height },
        });
        var consoleErrors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                consoleErrors.Add(message.Text);
            }
        };
        page.PageError += (_, error) => consoleErrors.Add(error);

        await ChartComponentFixturePage.InstallAsync(page, fixture.BaseUrl);
        var response = await page.GotoAsync(fixture.BaseUrl + ChartComponentFixturePage.PagePath);
        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        await page.WaitForFunctionAsync(InitialChartsCreated);

        return (page, consoleErrors);
    }

    public sealed class ChartColors
    {
        public string Background { get; set; } = string.Empty;

        public string Border { get; set; } = string.Empty;

        public string HoverBackground { get; set; } = string.Empty;

        public string HoverBorder { get; set; } = string.Empty;
    }

    public sealed class PaletteContrast
    {
        public string Label { get; set; } = string.Empty;

        public double Surface { get; set; }

        public double Hover { get; set; }
    }
}
