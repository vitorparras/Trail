namespace Trail.Web.Planning;

public sealed record PlanLink(
    string Text,
    string Page,
    IReadOnlyDictionary<string, string> RouteValues,
    PlanLinkIcon Icon = PlanLinkIcon.None);
