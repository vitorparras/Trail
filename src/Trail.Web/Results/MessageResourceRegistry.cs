using Microsoft.Extensions.Localization;

namespace Trail.Web.Results;

public static class MessageResourceRegistry
{
    private static readonly KeyValuePair<string, Type>[] ModulePrefixes =
    [
        new("practice.", typeof(PracticeResource)),
        new("fitness.", typeof(FitnessResource)),
        new("studytime.", typeof(StudyTimeResource)),
        new("pwa.", typeof(PwaResource)),
    ];

    public static IReadOnlyList<Type> ResourceTypes { get; } =
        [typeof(SharedResource), .. ModulePrefixes.Select(entry => entry.Value)];

    public static Type ResourceTypeFor(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        foreach (var (prefix, resourceType) in ModulePrefixes)
        {
            if (code.StartsWith(prefix, StringComparison.Ordinal))
            {
                return resourceType;
            }
        }

        return typeof(SharedResource);
    }

    public static IStringLocalizer CreateForCode(this IStringLocalizerFactory factory, string code)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.Create(ResourceTypeFor(code));
    }
}
