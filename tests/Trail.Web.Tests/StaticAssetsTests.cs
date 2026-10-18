using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Trail.Web.Tests;

public sealed partial class StaticAssetsTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
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
    public void Views_UseOnlyIconsPresentInSprite()
    {
        var repository = VendoredFilesTests.RepositoryRoot();
        var symbols = XDocument.Load(Path.Combine(repository.FullName, "src", "Trail.Web", "wwwroot", "lib", "lucide", "sprite.svg"))
            .Root!.Elements()
            .Select(symbol => (string?)symbol.Attribute("id"))
            .ToHashSet();
        var usages = Directory.EnumerateFiles(Path.Combine(repository.FullName, "src", "Trail.Web"), "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(path => IconTag().Matches(File.ReadAllText(path)).Select(tag => (
                Path: Path.GetRelativePath(repository.FullName, path),
                Tag: tag.Value,
                Name: IconName().Match(tag.Value).Groups["name"].Value)))
            .ToList();
        Assert.NotEmpty(usages);

        var invalid = usages
            .Where(usage => usage.Name.Length == 0 || usage.Name.Contains('@', StringComparison.Ordinal) || !symbols.Contains(usage.Name))
            .Select(usage => $"{(usage.Name.Length == 0 ? "(empty)" : usage.Name)} in {usage.Path}: {usage.Tag}")
            .ToList();

        Assert.True(invalid.Count == 0, string.Join(Environment.NewLine, invalid));
    }

    [GeneratedRegex(@"<icon\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex IconTag();

    [GeneratedRegex(@"(?<![\w-])name=""(?<name>[^""]*)""")]
    private static partial Regex IconName();
}
