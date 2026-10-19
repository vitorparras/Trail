namespace Trail.Web.Tests.Forms;

public sealed class FormErrorsScriptTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    [Fact]
    public async Task Get_Home_LoadsTheFormErrorsScriptDeferredWithANonce()
    {
        using var client = HomePage.CreateClient(factory);
        using var document = await HomePage.LoadAsync(client);

        var script = Assert.Single(document.Scripts, candidate => candidate.Source?.Contains("/js/form-errors.", StringComparison.Ordinal) == true);

        Assert.True(script.HasAttribute("defer"));
        Assert.False(string.IsNullOrEmpty(script.GetAttribute("nonce")));
    }

    [Fact]
    public async Task Get_FormErrorsScript_IsServedAsJavaScriptAndFollowsTheHtmxFourEvents()
    {
        using var client = HomePage.CreateClient(factory);

        using var response = await client.GetAsync(new Uri("/js/form-errors.js", UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        var script = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("htmx:after:swap", script, StringComparison.Ordinal);
        Assert.Contains("aria-invalid", script, StringComparison.Ordinal);
        Assert.DoesNotContain("eval(", script, StringComparison.Ordinal);
        Assert.DoesNotContain("new Function", script, StringComparison.Ordinal);
    }
}
