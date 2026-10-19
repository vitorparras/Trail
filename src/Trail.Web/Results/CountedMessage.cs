using Microsoft.Extensions.Localization;

namespace Trail.Web.Results;

public static class CountedMessage
{
    public const string OneSuffix = ".one";
    public const string OtherSuffix = ".other";

    public static IReadOnlyList<string> KeysFor(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return [code + OneSuffix, code + OtherSuffix];
    }

    public static string KeyFor(string code, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return code + (count == 1 ? OneSuffix : OtherSuffix);
    }

    public static LocalizedString LocalizeCount(this IStringLocalizerFactory factory, string code, int count)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.CreateForCode(code)[KeyFor(code, count), count];
    }
}
