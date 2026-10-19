using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Trail.Web.Charts;
using Trail.Web.TagHelpers;

namespace Trail.Web.Tests.Charts;

public sealed class ChartTagHelperTests : IDisposable
{
    private static readonly string[] Labels = ["Topic <A>", "Topic B & C", "Topic D"];

    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;

    public ChartTagHelperTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _originalCulture;
    }

    [Fact]
    public void Process_WithAModel_RendersCanvasAndTableFromTheSameModel()
    {
        var model = new ChartModel(
            "Performance by topic",
            "Topic",
            Labels,
            [
                new ChartSeries("Performance", [42.94m, null, 100m]),
                new ChartSeries("Median", [50m, 50m, 50m], ChartSeriesKind.Line, ChartColor.Secondary, Dashed: true),
            ])
        {
            Horizontal = true,
            SpanGaps = false,
            ValueAxis = ChartAxis.Symmetric(100),
            Format = ChartValueFormat.Percent(),
        };

        var html = Render(model);

        var canvas = Assert.Single(html.QuerySelectorAll("canvas[data-chart]"));
        Assert.Equal("img", canvas.GetAttribute("role"));
        Assert.Equal("Performance by topic", canvas.GetAttribute("aria-label"));
        Assert.Equal("chart-frame", canvas.ParentElement!.ClassName);

        using var json = JsonDocument.Parse(canvas.GetAttribute("data-chart")!);
        var root = json.RootElement;
        Assert.True(root.GetProperty("horizontal").GetBoolean());
        Assert.False(root.GetProperty("spanGaps").GetBoolean());
        Assert.Equal(Labels, root.GetProperty("labels").EnumerateArray().Select(label => label.GetString()));
        Assert.Equal(-100m, root.GetProperty("valueAxis").GetProperty("min").GetDecimal());
        Assert.Equal(100m, root.GetProperty("valueAxis").GetProperty("max").GetDecimal());
        Assert.Equal(1, root.GetProperty("format").GetProperty("decimals").GetInt32());
        Assert.Equal("%", root.GetProperty("format").GetProperty("suffix").GetString());

        var series = root.GetProperty("series").EnumerateArray().ToList();
        Assert.Equal(2, series.Count);
        Assert.Equal("bar", series[0].GetProperty("kind").GetString()!.ToLowerInvariant());
        Assert.Equal("line", series[1].GetProperty("kind").GetString()!.ToLowerInvariant());
        Assert.Equal("secondary", series[1].GetProperty("color").GetString()!.ToLowerInvariant());
        Assert.True(series[1].GetProperty("dashed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, series[0].GetProperty("values")[1].ValueKind);
        Assert.Equal(42.94m, series[0].GetProperty("values")[0].GetDecimal());

        var table = Assert.Single(html.QuerySelectorAll("table"));
        Assert.Equal("Performance by topic", table.QuerySelector("caption")!.TextContent);
        Assert.Equal(["Topic", "Performance", "Median"], table.QuerySelectorAll("thead th[scope='col']").Select(cell => cell.TextContent));

        var rows = table.QuerySelectorAll("tbody tr");
        Assert.Equal(Labels, rows.Select(row => row.QuerySelector("th[scope='row']")!.TextContent));
        Assert.Equal(["42,9%", "—", "100,0%"], rows.Select(row => row.QuerySelectorAll("td")[0].TextContent));
        Assert.Equal(["50,0%", "50,0%", "50,0%"], rows.Select(row => row.QuerySelectorAll("td")[1].TextContent));
    }

    [Fact]
    public void Process_WithAModel_EmitsNoInlineScriptNorStyleAttribute()
    {
        var html = Render(SimpleModel());

        Assert.Empty(html.QuerySelectorAll("script"));
        Assert.Empty(html.QuerySelectorAll("style"));
        Assert.DoesNotContain(html.All.SelectMany(element => element.Attributes), attribute =>
            attribute.Name == "style" || attribute.Name.StartsWith("on", StringComparison.Ordinal));
    }

    [Fact]
    public void Process_WithExistingClass_KeepsItAndAddsTheLayoutClass()
    {
        var html = Render(SimpleModel(), new TagHelperAttribute("class", "mt-4"));

        var root = html.QuerySelector("body > div")!;
        Assert.Contains("mt-4", root.ClassList);
        Assert.Contains("space-y-3", root.ClassList);
    }

    [Theory]
    [InlineData(1234.5, 1, "1.234,5")]
    [InlineData(0.25, 0, "0")]
    [InlineData(-37, 2, "-37,00")]
    public void Format_Number_UsesPortugueseSeparators(double value, int decimals, string expected)
    {
        Assert.Equal(expected, ChartValueFormat.Number(decimals).Format((decimal)value));
    }

    [Fact]
    public void Format_PercentPointsAndHours_AppendTheirSuffix()
    {
        Assert.Equal("42,9%", ChartValueFormat.Percent().Format(42.94m));
        Assert.Equal("12,5 pts", ChartValueFormat.Points("pts").Format(12.5m));
        Assert.Equal("2,5 h", ChartValueFormat.Hours().Format(2.5m));
    }

    [Fact]
    public void Constructor_WithASeriesOfTheWrongLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ChartModel("Title", "Header", Labels, [new ChartSeries("Series", [1m])]));
    }

    [Fact]
    public void Process_WithAModel_RendersTheExactSkeletonTheE2EFixtureMirrors()
    {
        var html = Render(SimpleModel());

        var root = html.QuerySelector("body > div")!;
        Assert.Equal(["div", "div"], root.Children.Select(child => child.LocalName));

        var frame = root.Children[0];
        Assert.Equal("chart-frame", frame.ClassName);
        var canvas = Assert.Single(frame.Children);
        Assert.Equal("canvas", canvas.LocalName);
        Assert.Equal(["aria-label", "data-chart", "role"], canvas.Attributes.Select(attribute => attribute.Name).Order(StringComparer.Ordinal));

        var region = root.Children[1];
        Assert.Equal("overflow-x-auto", region.ClassName);
        Assert.Equal("0", region.GetAttribute("tabindex"));
        Assert.Equal("group", region.GetAttribute("role"));
        Assert.Equal("Title", region.GetAttribute("aria-label"));
        var table = Assert.Single(region.Children);
        Assert.Equal("table", table.LocalName);
        Assert.Equal(["caption", "thead", "tbody"], table.Children.Select(child => child.LocalName));
    }

    [Fact]
    public void Constructor_WithANegativeOrTooLargeNumberOfDecimals_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartValueFormat.Number(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChartValueFormat(ChartValueFormat.MaxDecimals + 1, string.Empty));
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(10, -10)]
    public void Constructor_WithAnAxisWhoseMinimumIsNotBelowTheMaximum_Throws(int min, int max)
    {
        Assert.Throws<ArgumentException>(() => new ChartAxis(min, max));
    }

    [Fact]
    public void Symmetric_WithAZeroLimit_Throws()
    {
        Assert.Throws<ArgumentException>(() => ChartAxis.Symmetric(0));
    }

    [Fact]
    public void Constructor_WithoutSeries_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ChartModel("Title", "Header", Labels, []));
    }

    private static ChartModel SimpleModel() =>
        new("Title", "Header", ["One", "Two"], [new ChartSeries("Series", [1m, 2m])]);

    private static IHtmlDocument Render(ChartModel model, params TagHelperAttribute[] attributes)
    {
        var tagHelper = new ChartTagHelper { Model = model };
        var context = new TagHelperContext("chart", [.. attributes], new Dictionary<object, object>(), "chart-test");
        var output = new TagHelperOutput(
            "chart",
            [.. attributes],
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        tagHelper.Process(context, output);

        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);

        return new HtmlParser().ParseDocument(writer.ToString());
    }
}
