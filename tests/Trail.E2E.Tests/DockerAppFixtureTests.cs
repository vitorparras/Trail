using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Trail.E2E.Tests;

[Collection(E2ETestCollectionDefinition.Name)]
public sealed partial class DockerAppFixtureTests(DockerAppFixture fixture)
{
    private const string ProbePagePath = "/__service-worker-probe/";
    private const string ProbeWorkerPath = "/__service-worker-probe/worker.js";

    private const string ProbePage = """
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Service worker probe</title></head>
        <body><main><h1>Service worker probe</h1></main></body>
        </html>
        """;

    private const string ProbeWorker = "self.addEventListener('install', () => self.skipWaiting());";

    private const string RegisterProbeWorker = """
        async (url) => {
            await navigator.serviceWorker.register(url);
            return (await navigator.serviceWorker.getRegistrations()).length;
        }
        """;

    private const string ActivateProbeWorker = """
        async (url) => {
            const registration = await navigator.serviceWorker.register(url);
            const worker = registration.installing ?? registration.waiting ?? registration.active;
            while (worker.state !== 'activated') {
                if (worker.state === 'redundant') {
                    throw new Error('The probe worker became redundant.');
                }
                await new Promise(resolve => worker.addEventListener('statechange', resolve, { once: true }));
            }
            return worker.scriptURL;
        }
        """;

    private const string RegisteredWorkers = "async () => (await navigator.serviceWorker.getRegistrations()).length";

    [Fact]
    public async Task CreateUserAsync_TwoFreshUsers_DoNotSeeEachOtherPlans()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var first = await fixture.CreateUserAsync();
        var second = await fixture.CreateUserAsync();
        Assert.NotEqual(first.Email, second.Email);

        var firstPage = await fixture.CreatePageAsync(first, 1280, 800);
        var secondPage = await fixture.CreatePageAsync(second, 1280, 800);
        var firstPlan = $"Plan A {suffix}";
        var secondPlan = $"Plan B {suffix}";

        var firstPlanUrl = await CreatePlanAsync(firstPage, firstPlan);
        var secondPlanUrl = await CreatePlanAsync(secondPage, secondPlan);

        await ExpectPlanListAsync(firstPage, shown: firstPlan, hidden: secondPlan);
        await ExpectPlanListAsync(secondPage, shown: secondPlan, hidden: firstPlan);

        var foreignForSecond = await secondPage.GotoAsync(firstPlanUrl);
        Assert.NotNull(foreignForSecond);
        Assert.Equal(404, foreignForSecond.Status);

        var foreignForFirst = await firstPage.GotoAsync(secondPlanUrl);
        Assert.NotNull(foreignForFirst);
        Assert.Equal(404, foreignForFirst.Status);
    }

    [Fact]
    public async Task CreateOtherDevicePageAsync_SameUser_SeesTheUserDataInItsOwnSession()
    {
        var planName = $"Plan {Guid.NewGuid().ToString("N")[..8]}";
        var user = await fixture.CreateUserAsync();
        var page = await fixture.CreatePageAsync(user, 1280, 800);
        var planUrl = await CreatePlanAsync(page, planName);

        var otherDevice = await fixture.CreateOtherDevicePageAsync(user, new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 390, Height = 844 },
        });

        await ExpectPlanListAsync(otherDevice, shown: planName, hidden: null);

        var response = await otherDevice.GotoAsync(planUrl);
        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        await Expect(otherDevice.GetByRole(AriaRole.Main).GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(planName);

        Assert.NotSame(page.Context, otherDevice.Context);
        var firstSession = await E2EBrowserContexts.AuthenticationCookieAsync(page.Context);
        var otherSession = await E2EBrowserContexts.AuthenticationCookieAsync(otherDevice.Context);
        Assert.NotNull(firstSession);
        Assert.NotNull(otherSession);
        Assert.NotEqual(firstSession, otherSession);
    }

    [Fact]
    public async Task CreatePageAsync_AfterEarlierTests_ClosesTheirContextsAndKeepsTheCurrentTestOnes()
    {
        var page = await fixture.CreatePageAsync();
        var browser = page.Context.Browser;
        Assert.NotNull(browser);
        Assert.Single(browser.Contexts);

        await fixture.CreatePageAsync();
        Assert.Equal(2, browser.Contexts.Count);
    }

    [Fact]
    public async Task CreatePageAsync_ByDefault_RegistersNoServiceWorker()
    {
        var page = await fixture.CreatePageAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
        });

        await page.GotoAsync(fixture.BaseUrl + "/");
        Assert.Equal(0, await page.EvaluateAsync<int>(RegisteredWorkers));

        await InstallProbeAsync(page);
        await page.GotoAsync(fixture.BaseUrl + ProbePagePath);

        Assert.Equal(0, await page.EvaluateAsync<int>(RegisterProbeWorker, ProbeWorkerPath));
        Assert.True(await page.EvaluateAsync<bool>("() => navigator.serviceWorker.controller === null"));
    }

    [Fact]
    public async Task CreatePageAsync_WithServiceWorkersAllowed_RegistersTheWorker()
    {
        var page = await fixture.CreatePageAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            ServiceWorkers = ServiceWorkerPolicy.Allow,
        });

        await InstallProbeAsync(page);
        await page.GotoAsync(fixture.BaseUrl + ProbePagePath);

        var scriptUrl = await page.EvaluateAsync<string>(ActivateProbeWorker, ProbeWorkerPath);

        Assert.Equal(fixture.BaseUrl + ProbeWorkerPath, scriptUrl);
        Assert.Equal(1, await page.EvaluateAsync<int>(RegisteredWorkers));
    }

    private async Task InstallProbeAsync(IPage page)
    {
        await page.Context.RouteAsync(fixture.BaseUrl + ProbePagePath, route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "text/html; charset=utf-8",
            Body = ProbePage,
        }));

        await page.Context.RouteAsync(fixture.BaseUrl + ProbeWorkerPath, route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "text/javascript; charset=utf-8",
            Body = ProbeWorker,
        }));
    }

    private async Task<string> CreatePlanAsync(IPage page, string planName)
    {
        var main = page.GetByRole(AriaRole.Main);

        await page.GotoAsync(fixture.BaseUrl + "/planos/novo");
        await main.GetByLabel(UiText.Get("PlanName"), new() { Exact = true }).FillAsync(planName);
        await main.GetByRole(AriaRole.Button, new() { Name = UiText.Get("Save"), Exact = true }).ClickAsync();

        await Expect(page).ToHaveURLAsync(PlanDetailsPathPattern());
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(planName);

        return page.Url;
    }

    private async Task ExpectPlanListAsync(IPage page, string shown, string? hidden)
    {
        var main = page.GetByRole(AriaRole.Main);

        await page.GotoAsync(fixture.BaseUrl + "/planos");
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(UiText.Get("StudyPlans"));
        await Expect(main.GetByRole(AriaRole.Link, new() { Name = shown, Exact = true })).ToBeVisibleAsync();

        if (hidden is not null)
        {
            await Expect(main.GetByText(hidden)).ToHaveCountAsync(0);
        }
    }

    [GeneratedRegex("/planos/[0-9a-f-]{36}$")]
    private static partial Regex PlanDetailsPathPattern();
}
