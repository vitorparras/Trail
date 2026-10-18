using System.Net;
using Trail.Web.Dashboard;
using Trail.Web.Htmx;
using Trail.Web.Pages;

namespace Trail.Web.Tests.Accounts;

internal sealed record DashboardRouteCase(
    string Name,
    Type Page,
    string Handler,
    string Path,
    bool Htmx,
    HttpStatusCode ExpectedStatus,
    string? ExpectedPlanName);

[IsolationSweep("Trail.Web.Pages")]
internal static class DashboardIsolationSweep
{
    public const string MalformedPlan = "not-a-plan";

    public static IReadOnlyList<DashboardRouteCase> Build(SeededPlan victim, SeededPlan attacker, Guid unknownPlanId)
    {
        ArgumentNullException.ThrowIfNull(victim);
        ArgumentNullException.ThrowIfNull(attacker);

        var own = attacker.PlanId.ToString();
        var foreign = victim.PlanId.ToString();

        return
        [
            Get("Dashboard with the default plan", [], htmx: false, HttpStatusCode.OK, attacker.PlanName),
            Get("Dashboard with the default plan as partial", [], htmx: true, HttpStatusCode.OK, attacker.PlanName),
            Get("Dashboard with own plan", [own], htmx: false, HttpStatusCode.OK, attacker.PlanName),
            Get("Dashboard with own plan as partial", [own], htmx: true, HttpStatusCode.OK, attacker.PlanName),
            Get("Dashboard with own plan followed by foreign plan", [own, foreign], htmx: false, HttpStatusCode.OK, attacker.PlanName),
            Get("Dashboard with foreign plan", [foreign], htmx: false, HttpStatusCode.NotFound, null),
            Get("Dashboard with foreign plan as partial", [foreign], htmx: true, HttpStatusCode.NotFound, null),
            Get("Dashboard with foreign plan followed by own plan", [foreign, own], htmx: false, HttpStatusCode.NotFound, null),
            Get("Dashboard with unknown plan", [unknownPlanId.ToString()], htmx: false, HttpStatusCode.NotFound, null),
            Get("Dashboard with malformed plan", [MalformedPlan], htmx: true, HttpStatusCode.NotFound, null),
        ];
    }

    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, DashboardRouteCase routeCase)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(routeCase);

        using var request = new HttpRequestMessage(HttpMethod.Get, routeCase.Path);
        if (routeCase.Htmx)
        {
            request.Headers.Add(HtmxIntegration.RequestTypeHeaderName, HtmxIntegration.PartialRequestType);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static DashboardRouteCase Get(
        string name,
        IReadOnlyList<string> plans,
        bool htmx,
        HttpStatusCode expected,
        string? expectedPlanName)
    {
        var query = string.Join("&", plans.Select(plan => $"{DashboardRoute.PlanParameter}={Uri.EscapeDataString(plan)}"));
        var path = query.Length == 0 ? "/" : $"/?{query}";

        return new DashboardRouteCase(name, typeof(IndexModel), string.Empty, path, htmx, expected, expectedPlanName);
    }
}
