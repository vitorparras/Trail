namespace Trail.Web.Tests.Charts;

public sealed class ChartScriptsTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    [Fact]
    public async Task Get_Home_LoadsChartLibraryBeforeChartsScriptBothDeferred()
    {
        using var client = HomePage.CreateClient(factory);
        using var document = await HomePage.LoadAsync(client);

        var scripts = document.Scripts.ToList();
        var library = scripts.FindIndex(script => script.Source!.Contains("/lib/chart.js/chart.umd.min.", StringComparison.Ordinal));
        var charts = scripts.FindIndex(script => script.Source!.Contains("/js/charts.", StringComparison.Ordinal));
        var htmx = scripts.FindIndex(script => script.Source!.Contains("/lib/htmx/htmx.min.", StringComparison.Ordinal));

        Assert.True(library >= 0);
        Assert.True(charts > library);
        Assert.True(htmx >= 0);
        Assert.True(scripts[library].HasAttribute("defer"));
        Assert.True(scripts[charts].HasAttribute("defer"));
        Assert.False(string.IsNullOrEmpty(scripts[charts].GetAttribute("nonce")));
    }

    [Fact]
    public async Task Get_ChartsScript_IsServedAsJavaScript()
    {
        using var client = HomePage.CreateClient(factory);

        using var response = await client.GetAsync(new Uri("/js/charts.js", UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        var script = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("htmx:after:process", script, StringComparison.Ordinal);
        Assert.Contains("htmx:before:cleanup", script, StringComparison.Ordinal);
        Assert.DoesNotContain("eval(", script, StringComparison.Ordinal);
        Assert.DoesNotContain("new Function", script, StringComparison.Ordinal);
    }
}
