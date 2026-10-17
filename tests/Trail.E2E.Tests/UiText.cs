using System.Globalization;
using System.Xml.Linq;

namespace Trail.E2E.Tests;

internal static class UiText
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Entries = new(Load);

    public static string Get(string key) =>
        Entries.Value.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"The resource '{key}' was not found in SharedResource.resx.");

    public static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.GetCultureInfo("pt-BR"), Get(key), arguments);

    private static Dictionary<string, string> Load()
    {
        var path = Path.Combine(RepositoryPaths.Root, "src", "Trail.Web", "Resources", "SharedResource.resx");
        var document = XDocument.Load(path);

        return document.Root!
            .Elements("data")
            .ToDictionary(
                data => (string)data.Attribute("name")!,
                data => (string?)data.Element("value") ?? string.Empty,
                StringComparer.Ordinal);
    }
}
