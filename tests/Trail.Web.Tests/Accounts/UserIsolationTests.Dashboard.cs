using System.Net;
using System.Reflection;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Domain.Common;
using Trail.Infrastructure.Persistence;
using Trail.Web.Pages;
using Trail.Web.Tests.Persistence;

namespace Trail.Web.Tests.Accounts;

public sealed partial class UserIsolationTests
{
    private const string RootPageNamespace = "Trail.Web.Pages";

    private static readonly (Type Page, string MethodName)[] RootPageHandlersWithoutResourceIdentifiers =
    [
        (typeof(ErrorModel), nameof(ErrorModel.OnGet)),
        (typeof(ErrorModel), nameof(ErrorModel.OnPost)),
    ];

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task DashboardRoutes_WithAnotherUsersPlan_ReturnNotFoundWithoutLeakingOrChangingData(DatabaseProvider provider)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var factory = TrailWebApplicationFactory.Create(provider, new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = "",
            ["Registration:Mode"] = "Open"
        });

        using var clientA = CreateClient(factory);
        using var clientB = CreateClient(factory);

        var userAId = await RegisterConfirmAndSignInAsync(factory, clientA, UserAEmail, UserAPassword, HttpStatusCode.Redirect);
        var userBId = await RegisterConfirmAndSignInAsync(factory, clientB, UserBEmail, UserBPassword, HttpStatusCode.OK);

        var victim = await SeedCompletePlanAsync(factory, provider, UserId.From(userAId), "Alpha", cancellationToken);
        var attacker = await SeedCompletePlanAsync(factory, provider, UserId.From(userBId), "Bravo", cancellationToken);

        foreach (var routeCase in DashboardIsolationSweep.Build(victim, attacker, Guid.NewGuid()))
        {
            using var response = await DashboardIsolationSweep.SendAsync(clientB, routeCase);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            Assert.True(
                response.StatusCode == routeCase.ExpectedStatus,
                $"{routeCase.Name} (GET {routeCase.Path}) answered {(int)response.StatusCode} instead of {(int)routeCase.ExpectedStatus}.");

            foreach (var sensitiveText in victim.SensitiveTexts)
            {
                Assert.False(
                    body.Contains(sensitiveText, StringComparison.Ordinal),
                    $"{routeCase.Name} (GET {routeCase.Path}) exposed '{sensitiveText}'.");
            }

            var inspectedMarkup = routeCase.Htmx ? body : DashboardMarkupWithoutEchoedDialogActions(body);
            Assert.False(
                inspectedMarkup.Contains(victim.PlanId.ToString(), StringComparison.OrdinalIgnoreCase),
                $"{routeCase.Name} (GET {routeCase.Path}) exposed the victim's plan identifier.");

            if (routeCase.ExpectedPlanName is not null)
            {
                Assert.Contains(routeCase.ExpectedPlanName, body, StringComparison.Ordinal);
            }
        }

        await AssertSeededPlanUnchangedAsync(factory, provider, UserId.From(userAId), victim, cancellationToken);
        await AssertSeededPlanUnchangedAsync(factory, provider, UserId.From(userBId), attacker, cancellationToken);
        await AssertNoRowsBeyondSeedAsync(factory, provider, cancellationToken);

        var (victimDashboardResponse, victimDashboard, _, _) = await AccountTestHelper.GetDocWithAntiforgeryAsync(clientA, $"/?plano={victim.PlanId}");
        Assert.Equal(HttpStatusCode.OK, victimDashboardResponse.StatusCode);
        Assert.Contains(victim.PlanName, victimDashboard.Body!.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(attacker.PlanName, victimDashboard.Body.TextContent, StringComparison.Ordinal);
        Assert.Contains(
            victim.PlanId.ToString(),
            DashboardMarkupWithoutEchoedDialogActions(victimDashboard.DocumentElement.OuterHtml),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DashboardIsolationSweep_CoversEveryRootPageHandler()
    {
        var handlers = DiscoverRootPageHandlers();
        Assert.NotEmpty(handlers);

        var sweepKeys = DashboardIsolationSweep
            .Build(SeededPlan.Placeholder("Victim"), SeededPlan.Placeholder("Attacker"), Guid.NewGuid())
            .Select(routeCase => HandlerKey(routeCase.Page, HttpMethod.Get.Method, routeCase.Handler))
            .ToHashSet(StringComparer.Ordinal);

        var exemptKeys = RootPageHandlersWithoutResourceIdentifiers
            .Select(handler => handlers.Single(h => h.Page == handler.Page && h.Method.Name == handler.MethodName))
            .Select(handler => handler.Key)
            .ToHashSet(StringComparer.Ordinal);

        var missing = handlers
            .Where(handler => !sweepKeys.Contains(handler.Key) && !exemptKeys.Contains(handler.Key))
            .Select(handler => $"{handler.Page.Name}.{handler.Method.Name}")
            .ToList();

        var unknown = sweepKeys
            .Where(key => handlers.All(handler => handler.Key != key))
            .ToList();

        Assert.True(missing.Count == 0, "Handlers missing from the dashboard isolation sweep: " + string.Join(", ", missing));
        Assert.True(unknown.Count == 0, "Sweep cases targeting handlers that do not exist: " + string.Join(", ", unknown));
    }

    [Fact]
    public void RootPageHandlersWithoutResourceIdentifiers_TakeNoIdentifiers()
    {
        foreach (var (page, methodName) in RootPageHandlersWithoutResourceIdentifiers)
        {
            var method = page.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.NotNull(method);
            Assert.Equal(RootPageNamespace, page.Namespace);
            Assert.DoesNotContain(method.GetParameters(), parameter => IsIdentifier(parameter.ParameterType));
            Assert.DoesNotContain(
                page.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.CanWrite && IsIdentifier(property.PropertyType));
        }
    }

    private static string DashboardMarkupWithoutEchoedDialogActions(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        foreach (var dialogForm in document.QuerySelectorAll("form[method='dialog']"))
        {
            dialogForm.RemoveAttribute("action");
        }

        return document.DocumentElement.OuterHtml;
    }

    private static List<(Type Page, MethodInfo Method, string Key)> DiscoverRootPageHandlers()
    {
        var webAssembly = typeof(IndexModel).Assembly;
        var pageTypes = webAssembly
            .GetTypes()
            .Where(type => type.Namespace == RootPageNamespace
                && type.IsClass
                && !type.IsAbstract
                && typeof(PageModel).IsAssignableFrom(type));

        var handlers = new List<(Type Page, MethodInfo Method, string Key)>();
        foreach (var pageType in pageTypes)
        {
            foreach (var method in pageType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(method => method.DeclaringType?.Assembly == webAssembly))
            {
                var match = HandlerMethodPattern().Match(method.Name);
                if (match.Success)
                {
                    var verb = match.Groups["verb"].Value.ToUpperInvariant();
                    handlers.Add((pageType, method, HandlerKey(pageType, verb, match.Groups["handler"].Value)));
                }
            }
        }

        return handlers;
    }
}
