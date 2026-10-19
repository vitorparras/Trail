using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Localization;
using Trail.Application.Planning;
using Trail.Domain.Planning;
using Trail.Web.Planning;
using Trail.Web.Tests.Accounts;

namespace Trail.Web.Tests.Planning;

public sealed partial class PlanLinkTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    [Fact]
    public async Task PlanPage_WithContributors_ShowsTheLinksTheyChooseForThePlanInOrder()
    {
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        var withFitness = await AddPlanAsync(store, user, "Plan with fitness test", fitnessTestEnabled: true);
        using var factory = CreateFactory(store, user);
        using var client = CreateClient(factory);

        var (response, document, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, $"/planos/{withFitness.Id.Value}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var nav = Navigation(factory, document);
        var links = nav.QuerySelectorAll("a");
        Assert.Equal([SampleAlwaysLink.Text, SampleFitnessLink.Text], links.Select(link => link.TextContent.Trim()));
        Assert.Equal($"/planos/{withFitness.Id.Value}/editar", links[0].GetAttribute("href"));
        Assert.Equal($"/planos/{withFitness.Id.Value}?filterStatus=Studying", links[1].GetAttribute("href"));
        Assert.Null(links[0].QuerySelector("svg"));
        var icon = links[1].QuerySelector("svg[aria-hidden='true'] use");
        Assert.NotNull(icon);
        Assert.EndsWith("#dumbbell", icon.GetAttribute("href"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlanPage_WhenAContributorHidesItsLink_ShowsOnlyTheOthers()
    {
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        var withoutFitness = await AddPlanAsync(store, user, "Plan without fitness test", fitnessTestEnabled: false);
        using var factory = CreateFactory(store, user);
        using var client = CreateClient(factory);

        var (_, document, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, $"/planos/{withoutFitness.Id.Value}");

        var links = Navigation(factory, document).QuerySelectorAll("a");
        var link = Assert.Single(links);
        Assert.Equal(SampleAlwaysLink.Text, link.TextContent.Trim());
    }

    public static TheoryData<PlanLinkIcon> Icons => [.. Enum.GetValues<PlanLinkIcon>().Where(icon => icon != PlanLinkIcon.None)];

    [Theory]
    [MemberData(nameof(Icons))]
    public async Task PlanPage_LinkWithIcon_RendersTheSpriteSymbolNamedAfterTheIcon(PlanLinkIcon icon)
    {
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        var plan = await AddPlanAsync(store, user, "Plan with an icon link", fitnessTestEnabled: false);
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IPlanLinkContributor>(_ => new SampleIconLink(icon))));
        using var client = CreateClient(factory);

        var (_, document, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, $"/planos/{plan.Id.Value}");

        var link = Assert.Single(Navigation(factory, document).QuerySelectorAll("a"));
        var symbol = Assert.Single(link.QuerySelectorAll("svg[aria-hidden='true'] use"));
        Assert.EndsWith("#" + SymbolName(icon), symbol.GetAttribute("href"), StringComparison.Ordinal);
        Assert.Equal(SampleIconLink.Text, link.TextContent.Trim());
    }

    [Fact]
    public async Task PlanPage_WithoutContributors_HasNoLinkNavigation()
    {
        var store = new TestPlanningStore();
        var user = new TestCurrentUser();
        var plan = await AddPlanAsync(store, user, "Plan without links", fitnessTestEnabled: true);
        using var factory = PlanningTestHelper.CreateTestFactory(baseFactory, store, user);
        using var client = CreateClient(factory);

        var (response, document, _, _) = await PlanningTestHelper.GetDocWithAntiforgeryAsync(client, $"/planos/{plan.Id.Value}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(document.QuerySelector($"main nav[aria-label='{Label(factory)}']"));
    }

    private static IElement Navigation(WebApplicationFactory<Program> factory, IDocument document)
    {
        var nav = document.QuerySelector($"main nav[aria-label='{Label(factory)}']");
        Assert.NotNull(nav);
        return nav;
    }

    private static string Label(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>()["PlanLinks"].Value;

    private WebApplicationFactory<Program> CreateFactory(TestPlanningStore store, TestCurrentUser user) =>
        PlanningTestHelper.CreateTestFactory(baseFactory, store, user).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IPlanLinkContributor, SampleFitnessLink>();
                services.AddScoped<IPlanLinkContributor, SampleAlwaysLink>();
            }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<StudyPlan> AddPlanAsync(TestPlanningStore store, TestCurrentUser user, string name, bool fitnessTestEnabled)
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
            fitnessTestEnabled).Data;
        await store.AddStudyPlanAsync(plan, TestContext.Current.CancellationToken);
        return plan;
    }

    private static string SymbolName(PlanLinkIcon icon) =>
        SymbolWordBoundary().Replace(icon.ToString(), "-$1").ToLowerInvariant();

    [GeneratedRegex("(?<!^)([A-Z]|[0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SymbolWordBoundary();

    private sealed class SampleIconLink(PlanLinkIcon icon) : IPlanLinkContributor
    {
        public const string Text = "Sample icon";

        public int Order => 10;

        public PlanLink? GetLink(StudyPlanDetailView plan) =>
            new(Text, "/Plans/Edit", new Dictionary<string, string> { ["planId"] = plan.Id.Value.ToString() }, icon);
    }

    private sealed class SampleAlwaysLink : IPlanLinkContributor
    {
        public const string Text = "Sample always";

        public int Order => 10;

        public PlanLink? GetLink(StudyPlanDetailView plan) =>
            new(Text, "/Plans/Edit", new Dictionary<string, string> { ["planId"] = plan.Id.Value.ToString() });
    }

    private sealed class SampleFitnessLink : IPlanLinkContributor
    {
        public const string Text = "Sample fitness";

        public int Order => 20;

        public PlanLink? GetLink(StudyPlanDetailView plan) =>
            plan.FitnessTestEnabled
                ? new(
                    Text,
                    "/Plans/Details",
                    new Dictionary<string, string> { ["planId"] = plan.Id.Value.ToString(), ["filterStatus"] = "Studying" },
                    PlanLinkIcon.Dumbbell)
                : null;
    }
}
