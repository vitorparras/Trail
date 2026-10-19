using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Trail.E2E.Tests;

[Collection(E2ETestCollectionDefinition.Name)]
public sealed partial class DashboardE2ETests(DockerAppFixture fixture)
{
    [Theory]
    [InlineData(390, 844)]
    [InlineData(1280, 800)]
    public async Task PlanSelector_SwapsTheDashboardKeepingFocusAndAnnouncingThePlanWithoutAccessibilityViolations(int width, int height)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var firstPlanName = $"Painel A {suffix}";
        var secondPlanName = $"Painel B {suffix}";
        var user = await fixture.CreateUserAsync();
        var page = await fixture.CreatePageAsync(user, width, height);
        var main = page.GetByRole(AriaRole.Main);

        var firstPlanId = await CreatePlanAsync(page, firstPlanName);
        var secondPlanId = await CreatePlanAsync(page, secondPlanName);

        await page.GotoAsync($"{fixture.BaseUrl}/?plano={firstPlanId}");
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(UiText.Get("Dashboard"));
        var selector = main.GetByLabel(UiText.Get("DashboardPlan"), new() { Exact = true });
        var planHeading = main.Locator("#dashboard-plan-heading");
        await Expect(selector).ToHaveValueAsync(firstPlanId);
        await Expect(planHeading).ToHaveTextAsync(firstPlanName);
        await AxeTestHelper.AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);

        await selector.SelectOptionAsync(secondPlanId);
        var showButton = main.GetByRole(AriaRole.Button, new() { Name = UiText.Get("DashboardShowPlan"), Exact = true });
        await showButton.FocusAsync();
        var response = await page.RunAndWaitForResponseAsync(
            () => showButton.PressAsync("Enter"),
            candidate => candidate.Request.Method == "GET" && candidate.Url.Contains($"plano={secondPlanId}", StringComparison.Ordinal));

        Assert.Equal(200, response.Status);
        Assert.Equal("partial", await response.Request.HeaderValueAsync("hx-request-type"));
        await Expect(page).ToHaveURLAsync(new Regex($"/\\?plano={secondPlanId}$"));
        await Expect(planHeading).ToHaveTextAsync(secondPlanName);
        await Expect(main.GetByRole(AriaRole.Link, new() { Name = UiText.Get("DashboardOpenPlan") }))
            .ToHaveAttributeAsync("href", $"/planos/{secondPlanId}");
        await Expect(main.Locator("#dashboard-status[role='status']")).ToHaveTextAsync(UiText.Format("DashboardShowingPlan", secondPlanName));
        await Expect(showButton).ToBeFocusedAsync();
        await Expect(selector).ToHaveValueAsync(secondPlanId);
        await AxeTestHelper.AssertAxeZeroViolationsAcrossThemesAndViewportsAsync(page);

        await page.GoBackAsync();
        await Expect(page).ToHaveURLAsync(new Regex($"/\\?plano={firstPlanId}$"));
        await Expect(planHeading).ToHaveTextAsync(firstPlanName);
        await Expect(selector).ToHaveValueAsync(firstPlanId);
    }

    private async Task<string> CreatePlanAsync(IPage page, string planName)
    {
        var main = page.GetByRole(AriaRole.Main);

        await page.GotoAsync(fixture.BaseUrl + "/planos/novo");
        await main.GetByLabel(UiText.Get("PlanName"), new() { Exact = true }).FillAsync(planName);
        await main.GetByRole(AriaRole.Button, new() { Name = UiText.Get("Save"), Exact = true }).ClickAsync();

        await Expect(page).ToHaveURLAsync(PlanDetailsPathPattern());
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(planName);

        return PlanDetailsPathPattern().Match(page.Url).Groups["planId"].Value;
    }

    [GeneratedRegex("/planos/(?<planId>[0-9a-f-]{36})$")]
    private static partial Regex PlanDetailsPathPattern();
}
