using System.Globalization;
using System.Text.Json.Serialization;

namespace Trail.Web.Charts;

[JsonConverter(typeof(JsonStringEnumConverter<ChartSeriesKind>))]
public enum ChartSeriesKind
{
    Bar,
    Line,
}

[JsonConverter(typeof(JsonStringEnumConverter<ChartColor>))]
public enum ChartColor
{
    Primary,
    Secondary,
    Accent,
    Info,
    Success,
    Error,
}

public sealed record ChartAxis
{
    public ChartAxis(decimal min, decimal max)
    {
        if (min >= max)
        {
            throw new ArgumentException("The axis minimum must be lower than its maximum.", nameof(min));
        }

        Min = min;
        Max = max;
    }

    public decimal Min { get; }

    public decimal Max { get; }

    public static ChartAxis Symmetric(decimal limit) => new(-limit, limit);
}

public sealed record ChartValueFormat
{
    public const int MaxDecimals = 10;

    public ChartValueFormat(int decimals, string suffix)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(decimals, MaxDecimals);
        ArgumentNullException.ThrowIfNull(suffix);

        Decimals = decimals;
        Suffix = suffix;
    }

    public int Decimals { get; }

    public string Suffix { get; }

    public static ChartValueFormat Number(int decimals = 0) => new(decimals, string.Empty);

    public static ChartValueFormat Percent(int decimals = 1) => new(decimals, "%");

    public static ChartValueFormat Points(string unit, int decimals = 1) => new(decimals, " " + unit);

    public static ChartValueFormat Hours(int decimals = 1) => new(decimals, " h");

    public string Format(decimal value) =>
        value.ToString("N" + Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture) + Suffix;
}

public sealed record ChartSeries(
    string Label,
    IReadOnlyList<decimal?> Values,
    ChartSeriesKind Kind = ChartSeriesKind.Bar,
    ChartColor Color = ChartColor.Primary,
    bool Dashed = false);

public sealed class ChartModel
{
    public ChartModel(string title, string categoryHeader, IReadOnlyList<string> labels, IReadOnlyList<ChartSeries> series)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryHeader);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(series);

        if (series.Count == 0)
        {
            throw new ArgumentException("A chart needs at least one series.", nameof(series));
        }

        foreach (var item in series)
        {
            if (item.Values.Count != labels.Count)
            {
                throw new ArgumentException($"The series '{item.Label}' has {item.Values.Count} values for {labels.Count} labels.", nameof(series));
            }
        }

        Title = title;
        CategoryHeader = categoryHeader;
        Labels = labels;
        Series = series;
    }

    public string Title { get; }

    public string CategoryHeader { get; }

    public IReadOnlyList<string> Labels { get; }

    public IReadOnlyList<ChartSeries> Series { get; }

    public bool Horizontal { get; init; }

    public bool SpanGaps { get; init; }

    public ChartAxis? ValueAxis { get; init; }

    public ChartValueFormat Format { get; init; } = ChartValueFormat.Number();

    public string EmptyCellText { get; init; } = "—";
}
