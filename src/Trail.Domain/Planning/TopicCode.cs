
using System.Globalization;
using System.Text.RegularExpressions;
using Trail.Domain.Common;

namespace Trail.Domain.Planning;

public sealed partial class TopicCode : IEquatable<TopicCode>, IComparable<TopicCode>
{
    private TopicCode()
    {
    }

    private TopicCode(string value, IReadOnlyList<int> segments)
    {
        Value = value;
        Segments = segments;
    }

    public string Value { get; private set; } = string.Empty;

    public IReadOnlyList<int> Segments { get; private set; } = [];

    public TopicCode? ParentCode => Segments.Count <= 1
        ? null
        : new TopicCode(
            string.Join('.', Segments.Take(Segments.Count - 1)),
            Segments.Take(Segments.Count - 1).ToList());

    public static Result<TopicCode> Create(string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
        {
            return Result.Invalid(Message.Error("planning.topic.invalid_code", nameof(TopicCode)));
        }

        var trimmed = rawCode.Trim();
        while (trimmed.EndsWith('.'))
        {
            trimmed = trimmed[..^1].Trim();
        }

        if (string.IsNullOrEmpty(trimmed) || !CodePattern().IsMatch(trimmed))
        {
            return Result.Invalid(Message.Error("planning.topic.invalid_code", nameof(TopicCode)));
        }

        var parts = trimmed.Split('.');
        var segments = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            if (!int.TryParse(part, CultureInfo.InvariantCulture, out var segment) || segment < 0)
            {
                return Result.Invalid(Message.Error("planning.topic.invalid_code", nameof(TopicCode)));
            }

            segments.Add(segment);
        }

        var normalized = string.Join('.', segments);
        return Result.Ok(new TopicCode(normalized, segments));
    }

    public static (TopicCode? Code, string Title, bool HadInvalidNumberPrefix) ParseOutlineLine(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return (null, string.Empty, false);
        }

        var trimmed = rawLine.Trim();
        var match = OutlineLinePattern().Match(trimmed);
        if (match.Success)
        {
            var codeResult = Create(match.Groups[1].Value);
            if (codeResult.IsOk)
            {
                return (codeResult.Data, match.Groups[2].Value.Trim(), false);
            }

            return (null, trimmed, true);
        }

        var hadInvalid = InvalidPrefixPattern().IsMatch(trimmed);
        return (null, trimmed, hadInvalid);
    }

    public int CompareTo(TopicCode? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (other is null)
        {
            return 1;
        }

        var minLength = Math.Min(Segments.Count, other.Segments.Count);
        for (var i = 0; i < minLength; i++)
        {
            var comparison = Segments[i].CompareTo(other.Segments[i]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return Segments.Count.CompareTo(other.Segments.Count);
    }

    public bool Equals(TopicCode? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => obj is TopicCode other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(TopicCode? left, TopicCode? right) => Equals(left, right);

    public static bool operator !=(TopicCode? left, TopicCode? right) => !Equals(left, right);

    public static bool operator <(TopicCode? left, TopicCode? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(TopicCode? left, TopicCode? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >(TopicCode? left, TopicCode? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator >=(TopicCode? left, TopicCode? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;

    [GeneratedRegex(@"^\d+(\.\d+)*$")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^\s*(\d+(?:\.\d+)*)\.?\s+(.+)$")]
    private static partial Regex OutlineLinePattern();

    [GeneratedRegex(@"^\s*[\d\.]")]
    private static partial Regex InvalidPrefixPattern();
}
