namespace Trail.Web.Forms;

public static class RenderedFieldErrors
{
    private const string RenderedKey = "Trail.Forms.RenderedFieldErrors";
    private const string ReferencedKey = "Trail.Forms.ReferencedSpanIds";
    private const string MismatchKey = "Trail.Forms.SpanIdMismatches";

    public static void MarkRendered(HttpContext httpContext, string fieldKey)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        Rendered(httpContext).Add(fieldKey);
    }

    public static bool IsRendered(HttpContext httpContext, string fieldKey)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return Rendered(httpContext).Contains(fieldKey);
    }

    public static void RecordMismatch(HttpContext httpContext, string description)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        MismatchList(httpContext).Add(description);
    }

    public static IReadOnlyList<string> Mismatches(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return MismatchList(httpContext);
    }

    internal static Dictionary<string, string> ReferencedSpanIds(HttpContext httpContext) =>
        GetOrAdd(httpContext, ReferencedKey, () => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private static List<string> MismatchList(HttpContext httpContext) =>
        GetOrAdd(httpContext, MismatchKey, () => new List<string>());

    private static HashSet<string> Rendered(HttpContext httpContext) =>
        GetOrAdd(httpContext, RenderedKey, () => new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private static T GetOrAdd<T>(HttpContext httpContext, string key, Func<T> create)
        where T : class
    {
        if (httpContext.Items[key] is T existing)
        {
            return existing;
        }

        var created = create();
        httpContext.Items[key] = created;
        return created;
    }
}
