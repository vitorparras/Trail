using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Trail.Web.Charts;

namespace Trail.Web.TagHelpers;

[HtmlTargetElement("chart", Attributes = "model", TagStructure = TagStructure.NormalOrSelfClosing)]
public sealed class ChartTagHelper : TagHelper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ChartModel Model { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClass("space-y-3", HtmlEncoder.Default);
        output.Content.SetHtmlContent(RenderFrame());
        output.Content.AppendHtml(RenderTable());
    }

    private TagBuilder RenderFrame()
    {
        var canvas = new TagBuilder("canvas");
        canvas.Attributes["role"] = "img";
        canvas.Attributes["aria-label"] = Model.Title;
        canvas.Attributes["data-chart"] = JsonSerializer.Serialize(ChartPayload.From(Model), SerializerOptions);

        var frame = new TagBuilder("div");
        frame.AddCssClass("chart-frame");
        frame.InnerHtml.AppendHtml(canvas);

        return frame;
    }

    private TagBuilder RenderTable()
    {
        var caption = new TagBuilder("caption");
        caption.AddCssClass("sr-only");
        caption.InnerHtml.Append(Model.Title);

        var headerRow = new TagBuilder("tr");
        headerRow.InnerHtml.AppendHtml(Cell("th", Model.CategoryHeader, "col"));
        foreach (var series in Model.Series)
        {
            headerRow.InnerHtml.AppendHtml(Cell("th", series.Label, "col"));
        }

        var head = new TagBuilder("thead");
        head.InnerHtml.AppendHtml(headerRow);

        var body = new TagBuilder("tbody");
        for (var index = 0; index < Model.Labels.Count; index++)
        {
            var row = new TagBuilder("tr");
            row.InnerHtml.AppendHtml(Cell("th", Model.Labels[index], "row"));
            foreach (var series in Model.Series)
            {
                var value = series.Values[index];
                row.InnerHtml.AppendHtml(Cell("td", value is null ? Model.EmptyCellText : Model.Format.Format(value.Value), null));
            }

            body.InnerHtml.AppendHtml(row);
        }

        var table = new TagBuilder("table");
        table.AddCssClass("table table-sm w-full");
        table.InnerHtml.AppendHtml(caption);
        table.InnerHtml.AppendHtml(head);
        table.InnerHtml.AppendHtml(body);

        var scroller = new TagBuilder("div");
        scroller.AddCssClass("overflow-x-auto");
        scroller.Attributes["tabindex"] = "0";
        scroller.Attributes["role"] = "group";
        scroller.Attributes["aria-label"] = Model.Title;
        scroller.InnerHtml.AppendHtml(table);

        return scroller;
    }

    private static TagBuilder Cell(string tag, string text, string? scope)
    {
        var cell = new TagBuilder(tag);
        if (scope is not null)
        {
            cell.Attributes["scope"] = scope;
        }

        cell.InnerHtml.Append(text);

        return cell;
    }

    private sealed record ChartPayload(
        bool Horizontal,
        bool SpanGaps,
        IReadOnlyList<string> Labels,
        IReadOnlyList<ChartSeries> Series,
        ChartAxis? ValueAxis,
        ChartValueFormat Format)
    {
        public static ChartPayload From(ChartModel model) =>
            new(model.Horizontal, model.SpanGaps, model.Labels, model.Series, model.ValueAxis, model.Format);
    }
}
