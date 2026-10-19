using Trail.Web.Htmx;

namespace Trail.Web.Tests.Performance;

public sealed record PerformanceScenario(
    string Name,
    Func<SeededPerformancePlan, string> Path,
    Func<SeededPerformancePlan, IReadOnlyList<string>>? ExpectedTexts = null,
    bool HtmxPartial = false)
{
    internal HttpRequestMessage CreateRequest(SeededPerformancePlan plan)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Path(plan));
        if (HtmxPartial)
        {
            request.Headers.Add(HtmxIntegration.RequestTypeHeaderName, HtmxIntegration.PartialRequestType);
        }

        return request;
    }
}
