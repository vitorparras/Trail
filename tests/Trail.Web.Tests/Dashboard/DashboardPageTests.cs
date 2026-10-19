using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Localization;
using Trail.Application.Abstractions;
using Trail.Application.Dashboard;
using Trail.Domain.Planning;
using Trail.Web.Dashboard;
using Trail.Web.Htmx;
using Trail.Web.Pages;
using Trail.Web.Tests.Accounts;
using Trail.Web.Tests.Planning;

namespace Trail.Web.Tests.Dashboard;

public sealed partial class DashboardPageTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");

    [Fact]
    public async Task Get_Anonymous_ShowsThePresentationWithoutTheDashboard()
    {
        using var client = baseFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var localizer = baseFactory.Services.GetRequiredService<IStringLocalizer<IndexModel>>();

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var document = await ParseAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(localizer["Heading"].Value, document.QuerySelector("main h1")!.TextContent.Trim());
        Assert.Null(document.QuerySelector("#dashboard-plan"));
        Assert.Null(document.QuerySelector("#dashboard-content"));
    }

    [Fact]
    public async Task Get_AnonymousHtmxPartial_AsksHtmxToNavigateToLogin()
    {
        using var client = baseFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/?plano=" + Guid.NewGuid());
        request.Headers.Add(HtmxIntegration.RequestTypeHeaderName, HtmxIntegration.PartialRequestType);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(HtmxIntegration.RedirectHeaderName, out var redirects));
        Assert.StartsWith("/Account/Login", Assert.Single(redirects), StringComparison.Ordinal);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Get_WithoutPlans_WelcomesTheUserWithALinkToCreateTheFirstPlan()
    {
        using var factory = CreateFactory(new TestPlanningStore());
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var document = await ParseAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Text(factory, "DashboardWelcomeHeading"), document.QuerySelector("main h1")!.TextContent.Trim());
        Assert.Contains(Text(factory, "DashboardWelcomeDescription"), document.QuerySelector("main")!.TextContent, StringComparison.Ordinal);
        var createLink = Assert.Single(document.QuerySelectorAll("main a[href='/planos/novo']"));
        Assert.Equal(Text(factory, "DashboardCreateFirstPlan"), createLink.TextContent.Trim());
        Assert.Null(document.QuerySelector("#dashboard-plan"));
    }

    [Fact]
    public async Task Get_WithPlansAndNoBlocks_ShowsTheLabelledSelectorAndTheLinkToTheMostRecentlyActivePlan()
    {
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        var alpha = await AddPlanAsync(store, user, "Alpha plan");
        var bravo = await AddPlanAsync(store, user, "Bravo plan");
        var activity = new FixedPlanActivityContributor(new PlanActivity(bravo, new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)));

        using var factory = CreateFactory(store, user, activity);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var document = await ParseAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Text(factory, "Dashboard"), document.QuerySelector("main h1")!.TextContent.Trim());

        var form = document.QuerySelector("main form")!;
        Assert.Equal("get", form.GetAttribute("method"));
        Assert.Equal("/", form.GetAttribute("action"));
        Assert.Equal("/", form.GetAttribute("hx-get"));
        Assert.Equal("#dashboard-content", form.GetAttribute("hx-target"));
        Assert.Equal("true", form.GetAttribute("hx-push-url"));
        Assert.Equal(Text(factory, "DashboardShowPlan"), form.QuerySelector("button[type='submit']")!.TextContent.Trim());

        var select = (IHtmlSelectElement)form.QuerySelector("select#dashboard-plan")!;
        Assert.Equal(DashboardRoute.PlanParameter, select.Name);
        Assert.Equal(Text(factory, "DashboardPlan"), form.QuerySelector("label[for='dashboard-plan']")!.TextContent.Trim());
        Assert.Equal(
            [(alpha.Value.ToString(), "Alpha plan"), (bravo.Value.ToString(), "Bravo plan")],
            select.Options.Select(option => (option.Value, option.Text)).OrderBy(option => option.Text));
        Assert.Equal(bravo.Value.ToString(), Assert.Single(select.Options, option => option.IsSelected).Value);

        var status = document.QuerySelector("#dashboard-status")!;
        Assert.Equal("status", status.GetAttribute("role"));
        Assert.Empty(status.TextContent.Trim());

        var content = document.QuerySelector("#dashboard-content")!;
        Assert.Equal("Bravo plan", content.QuerySelector("h2#dashboard-plan-heading")!.TextContent.Trim());
        var planLink = Assert.Single(content.QuerySelectorAll($"a[href='/planos/{bravo.Value}']"));
        Assert.Equal(Text(factory, "DashboardOpenPlan"), planLink.TextContent.Trim());
        Assert.Equal("dashboard-plan-heading", planLink.GetAttribute("aria-describedby"));
        Assert.Contains(Text(factory, "DashboardNoBlocks"), content.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Alpha plan", content.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_WithOwnPlanInQuery_ShowsThatPlan()
    {
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        var alpha = await AddPlanAsync(store, user, "Alpha plan");
        var bravo = await AddPlanAsync(store, user, "Bravo plan");
        var activity = new FixedPlanActivityContributor(new PlanActivity(bravo, new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)));

        using var factory = CreateFactory(store, user, activity);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync($"/?plano={alpha.Value}", TestContext.Current.CancellationToken);
        var document = await ParseAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var select = (IHtmlSelectElement)document.QuerySelector("select#dashboard-plan")!;
        Assert.Equal(alpha.Value.ToString(), Assert.Single(select.Options, option => option.IsSelected).Value);
        Assert.Equal("Alpha plan", document.QuerySelector("#dashboard-plan-heading")!.TextContent.Trim());
        Assert.Single(document.QuerySelectorAll($"#dashboard-content a[href='/planos/{alpha.Value}']"));
    }

    [Theory]
    [InlineData("unknown", false)]
    [InlineData("unknown", true)]
    [InlineData("malformed", false)]
    [InlineData("malformed", true)]
    public async Task Get_WithUnknownOrMalformedPlan_ReturnsNotFound(string kind, bool htmx)
    {
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        await AddPlanAsync(store, user, "Alpha plan");

        using var factory = CreateFactory(store, user);
        using var client = CreateClient(factory);
        var plan = kind == "unknown" ? Guid.NewGuid().ToString() : "plan-42";

        using var response = await SendAsync(client, $"/?plano={plan}", htmx);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Alpha plan", body, StringComparison.Ordinal);
        Assert.Contains(Text(factory, "planning.plan.not_found"), body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_AsHtmxPartial_ReturnsOnlyTheDashboardContentAndAnnouncesTheChosenPlan()
    {
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        var alpha = await AddPlanAsync(store, user, "Alpha plan");
        await AddPlanAsync(store, user, "Bravo plan");

        using var factory = CreateFactory(store, user);
        using var client = CreateClient(factory);

        using var response = await SendAsync(client, $"/?plano={alpha.Value}", htmx: true);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var fragment = ParseFragment(html);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(HtmxIntegration.RequestTypeHeaderName, response.Headers.Vary);
        Assert.DoesNotContain("<html", html, StringComparison.OrdinalIgnoreCase);
        Assert.Null(fragment.QuerySelector("#dashboard-plan"));
        Assert.Equal("Alpha plan", fragment.QuerySelector("h2#dashboard-plan-heading")!.TextContent.Trim());
        Assert.Single(fragment.QuerySelectorAll($"a[href='/planos/{alpha.Value}']"));

        var announcement = Assert.Single(fragment.QuerySelectorAll("[hx-swap-oob]"));
        Assert.Equal("innerHTML:#dashboard-status", announcement.GetAttribute("hx-swap-oob"));
        Assert.Equal(string.Format(Culture, Text(factory, "DashboardShowingPlan"), "Alpha plan"), announcement.TextContent.Trim());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_WithSampleChartBlock_RendersTheChartWithTheChosenPlanContext(bool htmx)
    {
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        await AddPlanAsync(store, user, "Alpha plan");
        var bravo = await AddPlanAsync(store, user, "Bravo plan");

        using var factory = CreateFactory(store, user, blocks: [typeof(SampleChartDashboardBlock)]);
        using var client = CreateClient(factory);

        using var response = await SendAsync(client, $"/?plano={bravo.Value}", htmx);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var root = htmx ? ParseFragment(html) : new HtmlParser().ParseDocument(html).QuerySelector("#dashboard-content")!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var block = root.QuerySelector("#sample-chart-block");
        Assert.NotNull(block);
        Assert.Equal(bravo.Value.ToString(), block.GetAttribute("data-plan-id"));
        Assert.DoesNotContain(Text(factory, "DashboardNoBlocks"), root.TextContent, StringComparison.Ordinal);

        var title = SampleChartDashboardBlock.ChartTitle(new DashboardPlan(bravo, "Bravo plan"));
        var canvas = Assert.Single(block.QuerySelectorAll("canvas[data-chart]"));
        Assert.Equal(title, canvas.GetAttribute("aria-label"));
        using var chart = JsonDocument.Parse(canvas.GetAttribute("data-chart")!);
        var labels = chart.RootElement.GetProperty("labels").EnumerateArray().Select(label => label.GetString()).ToList();
        var values = chart.RootElement.GetProperty("series")[0].GetProperty("values").EnumerateArray().Select(value => value.GetDecimal()).ToList();
        Assert.Equal(["Alpha plan", "Bravo plan"], labels.Order(StringComparer.Ordinal));
        Assert.Equal(1m, values[labels.IndexOf("Bravo plan")]);
        Assert.Equal(0m, values[labels.IndexOf("Alpha plan")]);

        var table = Assert.Single(block.QuerySelectorAll("table"));
        Assert.Equal(title, table.QuerySelector("caption")!.TextContent);
        Assert.Equal(labels, table.QuerySelectorAll("tbody th[scope='row']").Select(cell => cell.TextContent));
    }

    [Fact]
    public async Task Get_WithSeveralBlocks_RendersThemInTheirOrder()
    {
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        var alpha = await AddPlanAsync(store, user, "Alpha plan");

        using var factory = CreateFactory(store, user, blocks: [typeof(SampleChartDashboardBlock), typeof(SampleNoteDashboardBlock)]);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var note = html.IndexOf($"{SampleNoteDashboardBlock.Marker}:{alpha.Value}", StringComparison.Ordinal);
        var chart = html.IndexOf("id=\"sample-chart-block\"", StringComparison.Ordinal);
        Assert.True(note >= 0, "The note block was not rendered with the chosen plan.");
        Assert.True(chart > note, "The blocks were not rendered in their order.");
    }

    [Fact]
    public async Task Get_WithABlockThatRendersNothing_AddsNoMarkup()
    {
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        var alpha = await AddPlanAsync(store, user, "Alpha plan");

        using var visibleOnly = CreateFactory(store, user, blocks: [typeof(SampleChartDashboardBlock), typeof(SampleNoteDashboardBlock)]);
        using var withHidden = CreateFactory(store, user, blocks: [typeof(SampleChartDashboardBlock), typeof(SampleHiddenDashboardBlock), typeof(SampleNoteDashboardBlock)]);
        using var visibleOnlyClient = CreateClient(visibleOnly);
        using var withHiddenClient = CreateClient(withHidden);

        var expected = await DashboardContentAsync(visibleOnlyClient, $"/?plano={alpha.Value}");
        var actual = await DashboardContentAsync(withHiddenClient, $"/?plano={alpha.Value}");

        Assert.Contains(SampleNoteDashboardBlock.Marker, actual, StringComparison.Ordinal);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Get_WithUnknownPlanAsHtmxPartial_AnnouncesTheMessageThroughTheToastLiveRegion()
    {
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        await AddPlanAsync(store, user, "Alpha plan");

        using var factory = CreateFactory(store, user);
        using var client = CreateClient(factory);

        using var page = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var document = await ParseAsync(page);
        using var response = await SendAsync(client, $"/?plano={Guid.NewGuid()}", htmx: true);
        var fragment = ParseFragment(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal("polite", document.QuerySelector("#toasts")!.GetAttribute("aria-live"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var toasts = Assert.Single(fragment.QuerySelectorAll("[hx-swap-oob='beforeend:#toasts']"));
        Assert.Equal(Text(factory, "planning.plan.not_found"), toasts.QuerySelector("[data-toast='error'] span")!.TextContent.Trim());
        Assert.Empty(fragment.QuerySelectorAll("[hx-swap-oob='innerHTML:#dashboard-status']"));
    }

    [Fact]
    public async Task Get_DashboardWithBlocks_HasNoStrictCspViolations()
    {
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        await AddPlanAsync(store, user, "Alpha plan");

        using var factory = CreateFactory(store, user, blocks: [typeof(SampleChartDashboardBlock), typeof(SampleNoteDashboardBlock)]);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var document = await ParseAsync(response);

        Assert.All(document.Scripts, script => Assert.False(string.IsNullOrEmpty(script.Source)));
        Assert.Empty(document.QuerySelectorAll("style"));
        var attributes = document.All.SelectMany(element => element.Attributes).Select(attribute => attribute.Name).ToList();
        Assert.DoesNotContain("style", attributes);
        Assert.DoesNotContain(attributes, name => name.StartsWith("on", StringComparison.OrdinalIgnoreCase) || name.StartsWith("hx-on", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DashboardBlocks_PointToViewComponentsThatTakeOnlyTheDashboardContext()
    {
        using var factory = CreateFactory(
            new TestPlanningStore(),
            blocks: [typeof(SampleChartDashboardBlock), typeof(SampleHiddenDashboardBlock), typeof(SampleNoteDashboardBlock)]);
        using var scope = factory.Services.CreateScope();
        var viewComponents = scope.ServiceProvider.GetRequiredService<IViewComponentDescriptorCollectionProvider>().ViewComponents.Items;

        var blocks = scope.ServiceProvider.GetServices<IDashboardBlock>().ToList();

        Assert.Contains(blocks, block => block is SampleChartDashboardBlock);
        Assert.Contains(blocks, block => block is SampleHiddenDashboardBlock);
        Assert.Contains(blocks, block => block is SampleNoteDashboardBlock);
        foreach (var block in blocks)
        {
            var descriptor = Assert.Single(viewComponents, component => component.TypeInfo.AsType() == block.ViewComponent);
            var parameter = Assert.Single(descriptor.Parameters);
            Assert.Equal(typeof(DashboardContext), parameter.ParameterType);
        }
    }

    private WebApplicationFactory<Program> CreateFactory(
        TestPlanningStore store,
        TestCurrentUser? user = null,
        IPlanActivityContributor? activity = null,
        IReadOnlyList<Type>? blocks = null)
    {
        return PlanningTestHelper
            .CreateTestFactory(baseFactory, store, user ?? new TestCurrentUser())
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.AddMvcCore().AddApplicationPart(typeof(DashboardPageTests).Assembly);

                if (activity is not null)
                {
                    services.AddSingleton(activity);
                }

                foreach (var block in blocks ?? [])
                {
                    services.AddScoped(typeof(IDashboardBlock), block);
                }
            }));
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<StudyPlanId> AddPlanAsync(TestPlanningStore store, TestCurrentUser user, string name)
    {
        var plan = StudyPlan.Create(
            user.Id,
            name,
            null,
            ScoringRule.Cebraspe,
            PerformancePeriod.All(),
            PerformanceBands.CreateDefault(),
            null,
            120,
            false).Data;

        await store.AddStudyPlanAsync(plan, TestContext.Current.CancellationToken);
        return plan.Id;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string url, bool htmx)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (htmx)
        {
            request.Headers.Add(HtmxIntegration.RequestTypeHeaderName, HtmxIntegration.PartialRequestType);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<string> DashboardContentAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = await ParseAsync(response);
        return WhitespaceRun().Replace(document.QuerySelector("#dashboard-content")!.InnerHtml, " ").Trim();
    }

    private static async Task<IHtmlDocument> ParseAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return new HtmlParser().ParseDocument(html);
    }

    private static IElement ParseFragment(string html) =>
        new HtmlParser().ParseDocument($"<!DOCTYPE html><html><body><div id=\"fragment\">{html}</div></body></html>").QuerySelector("#fragment")!;

    private static string Text(WebApplicationFactory<Program> factory, string key) =>
        factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>()[key].Value;

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRun();

    private sealed class FixedPlanActivityContributor(params PlanActivity[] activities) : IPlanActivityContributor
    {
        public Task<IReadOnlyList<PlanActivity>> GetLatestActivityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlanActivity>>(activities);
    }
}
