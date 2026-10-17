
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trail.Domain.Planning;

public static partial class TextNormalizer
{
    public static string NormalizeSearchText(params ReadOnlySpan<string?> parts)
    {
        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(part.Trim());
        }

        if (builder.Length == 0)
        {
            return string.Empty;
        }

        var normalizedString = builder.ToString().Normalize(NormalizationForm.FormD);
        var strippedBuilder = new StringBuilder(normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                strippedBuilder.Append(c);
            }
        }

        var lower = strippedBuilder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        return WhitespacePattern().Replace(lower, " ").Trim();
    }

    public static string NormalizeSearchText(params string?[] parts) =>
        NormalizeSearchText(parts.AsSpan());

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
