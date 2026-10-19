using System.Globalization;
using System.Xml.Linq;

namespace Trail.E2E.Tests;

internal static class UiText
{
    private static readonly KeyValuePair<string, UiResource>[] MessageCodePrefixes =
    [
        new("practice.", UiResource.Practice),
        new("fitness.", UiResource.Fitness),
        new("studytime.", UiResource.StudyTime),
        new("pwa.", UiResource.Pwa),
    ];

    private static readonly Lazy<IReadOnlyDictionary<UiResource, IReadOnlyDictionary<string, string>>> Entries = new(LoadAll);

    public static string Get(string key) => Get(ResourceForKey(key), key);

    public static string Get(UiResource resource, string key) =>
        Entries.Value[resource].TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"The resource '{key}' was not found in {FileName(resource)}.");

    public static string Format(string key, params object[] arguments) =>
        Format(ResourceForKey(key), key, arguments);

    public static string Format(UiResource resource, string key, params object[] arguments) =>
        string.Format(CultureInfo.GetCultureInfo("pt-BR"), Get(resource, key), arguments);

    private static UiResource ResourceForKey(string key)
    {
        foreach (var (prefix, resource) in MessageCodePrefixes)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                return resource;
            }
        }

        return UiResource.Shared;
    }

    private static string FileName(UiResource resource) => $"{resource}Resource.resx";

    private static Dictionary<UiResource, IReadOnlyDictionary<string, string>> LoadAll() =>
        Enum.GetValues<UiResource>().ToDictionary(resource => resource, Load);

    private static IReadOnlyDictionary<string, string> Load(UiResource resource)
    {
        var path = Path.Combine(RepositoryPaths.Root, "src", "Trail.Web", "Resources", FileName(resource));
        var document = XDocument.Load(path);

        return document.Root!
            .Elements("data")
            .ToDictionary(
                data => (string)data.Attribute("name")!,
                data => (string?)data.Element("value") ?? string.Empty,
                StringComparer.Ordinal);
    }
}
