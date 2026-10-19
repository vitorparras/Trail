using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;

namespace Trail.E2E.Tests;

internal static class ChartComponentFixturePage
{
    public const string PagePath = "/__chart-fixture";
    public const string RegionPath = "/__chart-fixture/region";
    public const string SlotPath = "/__chart-fixture/slot";
    public const string MorphPath = "/__chart-fixture/morph";

    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string[] Topics = ["Topic A", "Topic B", "Topic C"];

    private static readonly string[] Attempts = ["First attempt", "Second attempt", "Third attempt", "Fourth attempt"];

    private static readonly Series[] FitnessSeries =
    [
        new("Total points", [40m, 55m, 62m, 70m], "line", "primary"),
        new("Push-ups (repetitions)", [10m, 14m, 18m, 20m], "line", "secondary"),
        new("Sit-ups (repetitions)", [12m, 16m, 20m, 24m], "line", "accent"),
        new("Running (12 minutes)", [8m, 9m, 11m, 12m], "line", "info"),
        new("Swimming (50 meters)", [6m, 7m, 9m, 10m], "line", "success"),
        new("Failed attempts", [2m, 3m, 4m, 6m], "line", "error", true),
    ];

    public static async Task InstallAsync(IPage page, string baseUrl)
    {
        await page.RouteAsync(baseUrl + PagePath, route => Fulfill(route, Page()));
        await page.RouteAsync(baseUrl + RegionPath, route => Fulfill(route, Region(2)));
        await page.RouteAsync(baseUrl + MorphPath, route => Fulfill(route, Region(3)));
        await page.RouteAsync(baseUrl + SlotPath, route => Fulfill(route, Chart("chart-slot", "Slot chart", Topics, [new Series("Slot", [10m, 20m, 30m])])));
    }

    private static Task Fulfill(IRoute route, string body) =>
        route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "text/html; charset=utf-8",
            Headers = new Dictionary<string, string> { ["Content-Security-Policy"] = ContentSecurityPolicy },
            Body = body,
        });

    private static string Page() => $$"""
        <!doctype html>
        <html lang="pt-BR" data-theme="trail-light">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="htmx-config" content='{"includeIndicatorCSS":false}'>
            <title>Chart fixture</title>
            <link rel="icon" href="data:,">
            <script src="/js/theme.js"></script>
            <link rel="stylesheet" href="/css/app.css">
            <script src="/lib/htmx/htmx.min.js" defer></script>
            <script src="/lib/chart.js/chart.umd.min.js" defer></script>
            <script src="/js/charts.js" defer></script>
        </head>
        <body class="bg-base-200 text-base-content">
            <header class="p-4">
                <button type="button" class="btn btn-ghost" aria-pressed="false" data-theme-toggle>Toggle theme</button>
            </header>
            <main id="main-content" class="space-y-6 p-4">
                <h1 class="text-2xl font-bold">Chart fixture</h1>
                <div class="flex flex-wrap gap-2">
                    <button type="button" id="reload" class="btn">Reload region</button>
                    <button type="button" id="load-slot" class="btn" hx-get="{{SlotPath}}" hx-target="#slot" hx-swap="innerHTML">Load slot</button>
                    <button type="button" id="morph" class="btn" hx-get="{{MorphPath}}" hx-target="#region" hx-swap="outerMorph">Morph region</button>
                </div>
                {{Region(1)}}
                <div id="slot"></div>
                <details id="lazy">
                    <summary>Lazy chart</summary>
                    {{Chart("chart-lazy", "Lazy chart", Topics, [new Series("Lazy", [5m, 15m, 25m])])}}
                </details>
                <details id="wide">
                    <summary>Wide chart</summary>
                    {{Chart("chart-wide", "Fitness results by attempt", Attempts, FitnessSeries)}}
                </details>
                <details id="twins">
                    <summary>Charts sharing a title</summary>
                    {{Chart("chart-twin-a", "Weekly evolution", Topics, [new Series("Volume", [3m, 6m, 9m])])}}
                    {{Chart("chart-twin-b", "Weekly evolution", Topics, [new Series("Performance", [30m, 60m, 90m], "line")])}}
                </details>
            </main>
        </body>
        </html>
        """;

    private static string Region(int version)
    {
        var bars = version == 2 ? new decimal?[] { 70m, 10m, -20m } : new decimal?[] { 60m, null, -40m };

        return $$"""
            <section id="region" class="space-y-6" hx-get="{{RegionPath}}" hx-trigger="click from:#reload" hx-swap="outerHTML">
                <h2 class="text-xl font-semibold">Region {{version}}</h2>
                {{Chart("chart-bars", "Performance by topic", Topics, [new Series("Performance", bars)], horizontal: true, symmetricAxis: 100)}}
                {{Chart("chart-line", "Score evolution", Topics, [new Series("Score", [30m, null, 80m], "line"), new Series("Median", [50m, 50m, 50m], "line", "secondary", true)], symmetricAxis: 100)}}
            </section>
            """;
    }

    private static string Chart(string id, string title, string[] labels, Series[] series, bool horizontal = false, int? symmetricAxis = null)
    {
        var model = new
        {
            horizontal,
            spanGaps = false,
            labels,
            series = series.Select(item => new { label = item.Label, kind = item.Kind, color = item.Color, dashed = item.Dashed, values = item.Values }),
            valueAxis = symmetricAxis is null ? null : new { min = -symmetricAxis, max = symmetricAxis },
            format = new { decimals = 1, suffix = "%" },
        };
        var json = WebUtility.HtmlEncode(JsonSerializer.Serialize(model, SerializerOptions));
        var table = new StringBuilder();
        table.Append("<table class=\"table table-sm w-full\"><caption class=\"sr-only\">").Append(WebUtility.HtmlEncode(title)).Append("</caption><thead><tr><th scope=\"col\">Topic</th>");
        foreach (var item in series)
        {
            table.Append("<th scope=\"col\">").Append(WebUtility.HtmlEncode(item.Label)).Append("</th>");
        }

        table.Append("</tr></thead><tbody>");
        for (var index = 0; index < labels.Length; index++)
        {
            table.Append("<tr><th scope=\"row\">").Append(WebUtility.HtmlEncode(labels[index])).Append("</th>");
            foreach (var item in series)
            {
                table.Append("<td>").Append(item.Values[index]?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? "—").Append("</td>");
            }

            table.Append("</tr>");
        }

        table.Append("</tbody></table>");
        var encodedTitle = WebUtility.HtmlEncode(title);

        return $"""
            <div id="{id}" class="space-y-3">
                <div class="chart-frame"><canvas role="img" aria-label="{encodedTitle}" data-chart="{json}"></canvas></div>
                <div class="overflow-x-auto" tabindex="0" role="group" aria-label="{encodedTitle}">{table}</div>
            </div>
            """;
    }

    private sealed record Series(string Label, decimal?[] Values, string Kind = "bar", string Color = "primary", bool Dashed = false);
}
