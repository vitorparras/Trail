using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Trail.Web.Tests.Accounts;

public sealed partial class PageNamespaceIsolationTests
{
    private const string PagesNamespace = "Trail.Web.Pages";

    private static readonly Dictionary<string, string> JustifiedExemptions = new(StringComparer.Ordinal)
    {
        ["Trail.Web.Pages.Account"] =
            "Phase 1: AccountPagesTests and UserIsolationTests.CrossUserIsolation_AcrossQueryAndHttp_EnforcesIsolationAndDeactivation cover the account pages, which act only on the signed-in user.",
        ["Trail.Web.Pages.Admin"] =
            "Phase 1: AccountPagesTests and UserIsolationTests.CrossUserIsolation_AcrossQueryAndHttp_EnforcesIsolationAndDeactivation cover the administration pages, which only administrators reach.",
    };

    [Fact]
    public void PageNamespacesWithHandlers_HaveAnIsolationSweepOrAJustifiedExemption()
    {
        var uncovered = FindUncoveredNamespaces(PageNamespacesWithHandlers(), SweptNamespaces(), JustifiedExemptions.Keys);

        Assert.True(
            uncovered.Count == 0,
            "Page namespaces with handlers but without an isolation sweep ([IsolationSweep]) or a justified exemption: " + string.Join(", ", uncovered));
    }

    [Fact]
    public void IsolationSweeps_DeclarePageNamespacesThatHaveHandlers()
    {
        var pageNamespaces = PageNamespacesWithHandlers();

        var stale = SweptNamespaces().Where(ns => !pageNamespaces.Contains(ns)).ToList();

        Assert.True(stale.Count == 0, "Isolation sweeps declare namespaces without page handlers: " + string.Join(", ", stale));
    }

    [Fact]
    public void JustifiedExemptions_AreExplainedAndStillNeeded()
    {
        var pageNamespaces = PageNamespacesWithHandlers();
        var swept = SweptNamespaces();

        foreach (var (pageNamespace, justification) in JustifiedExemptions)
        {
            Assert.False(string.IsNullOrWhiteSpace(justification), $"The exemption of {pageNamespace} has no justification.");
            Assert.Contains(pageNamespace, pageNamespaces);
            Assert.DoesNotContain(pageNamespace, swept);
        }
    }

    [Fact]
    public void FindUncoveredNamespaces_ComparesNamespacesByEquality()
    {
        string[] pageNamespaces = [PagesNamespace, $"{PagesNamespace}.Plans", $"{PagesNamespace}.Practice", $"{PagesNamespace}.Practice.MockExams", $"{PagesNamespace}.Account"];
        string[] swept = [PagesNamespace, $"{PagesNamespace}.Practice"];
        string[] exempt = [$"{PagesNamespace}.Account"];

        var uncovered = FindUncoveredNamespaces(pageNamespaces, swept, exempt);

        Assert.Equal([$"{PagesNamespace}.Plans", $"{PagesNamespace}.Practice.MockExams"], uncovered);
    }

    private static List<string> FindUncoveredNamespaces(
        IEnumerable<string> pageNamespaces,
        IEnumerable<string> sweptNamespaces,
        IEnumerable<string> exemptNamespaces)
    {
        var covered = sweptNamespaces.Concat(exemptNamespaces).ToHashSet(StringComparer.Ordinal);

        return [.. pageNamespaces
            .Where(ns => !covered.Contains(ns))
            .Order(StringComparer.Ordinal)];
    }

    private static HashSet<string> PageNamespacesWithHandlers()
    {
        var webAssembly = typeof(Trail.Web.Pages.IndexModel).Assembly;

        return webAssembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, Namespace: not null }
                && (type.Namespace == PagesNamespace || type.Namespace.StartsWith(PagesNamespace + ".", StringComparison.Ordinal))
                && typeof(PageModel).IsAssignableFrom(type)
                && type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Any(method => method.DeclaringType?.Assembly == webAssembly && HandlerMethodPattern().IsMatch(method.Name)))
            .Select(type => type.Namespace!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> SweptNamespaces() =>
        typeof(PageNamespaceIsolationTests).Assembly
            .GetTypes()
            .SelectMany(type => type.GetCustomAttributes<IsolationSweepAttribute>())
            .Select(attribute => attribute.PageNamespace)
            .ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex("^On(Get|Post|Put|Delete|Patch)\\w*$", RegexOptions.CultureInvariant)]
    private static partial Regex HandlerMethodPattern();
}
