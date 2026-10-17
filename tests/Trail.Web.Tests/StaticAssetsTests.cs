using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Trail.Web.Tests;

public sealed class StaticAssetsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Get_AssetsReferencedByLayout_AreServed()
    {
        using var client = HomePage.CreateClient(factory);
        using var document = await HomePage.LoadAsync(client);

        var urls = document.Scripts.Select(script => script.Source)
            .Concat(document.QuerySelectorAll("link[rel='stylesheet']").Select(link => link.GetAttribute("href")))
            .Concat(document.QuerySelectorAll("use").Select(use => use.GetAttribute("href")?.Split('#')[0]))
            .OfType<string>()
            .Distinct()
            .ToList();

        Assert.Contains(urls, url => url.StartsWith("/css/app.", StringComparison.Ordinal));
        Assert.Contains(urls, url => url.StartsWith("/lib/htmx/htmx.min.", StringComparison.Ordinal));
        Assert.Contains(urls, url => url.StartsWith("/lib/lucide/sprite.", StringComparison.Ordinal));
        foreach (var url in urls)
        {
            using var response = await client.GetAsync(new Uri(url, UriKind.Relative), TestContext.Current.CancellationToken);
            Assert.True(response.IsSuccessStatusCode, url);
            Assert.True(response.Content.Headers.ContentLength > 0, url);
        }
    }

    [Theory]
    [InlineData("/css/app.css", "text/css")]
    [InlineData("/lib/htmx/htmx.min.js", "text/javascript")]
    [InlineData("/lib/chart.js/chart.umd.min.js", "text/javascript")]
    [InlineData("/lib/lucide/sprite.svg", "image/svg+xml")]
    public async Task Get_VendoredAndGeneratedAssets_AreServedWithContentType(string url, string contentType)
    {
        using var client = HomePage.CreateClient(factory);

        using var response = await client.GetAsync(new Uri(url, UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_Stylesheet_ContainsLightAndDarkThemes()
    {
        using var client = HomePage.CreateClient(factory);

        var css = await client.GetStringAsync(new Uri("/css/app.css", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Contains("[data-theme=trail-light]", css, StringComparison.Ordinal);
        Assert.Contains("[data-theme=trail-dark]", css, StringComparison.Ordinal);
        Assert.Contains("prefers-color-scheme:dark", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_Home_UsesOnlyIconsPresentInSprite()
    {
        using var client = HomePage.CreateClient(factory);
        using var document = await HomePage.LoadAsync(client);
        var references = document.QuerySelectorAll("svg[aria-hidden='true'] > use")
            .Select(use => use.GetAttribute("href")!.Split('#'))
            .ToList();
        Assert.NotEmpty(references);

        foreach (var spriteUrl in references.Select(reference => reference[0]).Distinct())
        {
            var sprite = XDocument.Parse(await client.GetStringAsync(new Uri(spriteUrl, UriKind.Relative), TestContext.Current.CancellationToken));
            var symbols = sprite.Root!.Elements().Select(symbol => (string?)symbol.Attribute("id")).ToHashSet();

            Assert.All(references.Where(reference => reference[0] == spriteUrl), reference => Assert.Contains(reference[1], symbols));
        }
    }
}
