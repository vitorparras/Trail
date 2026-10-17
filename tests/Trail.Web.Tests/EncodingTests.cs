using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Localization;

namespace Trail.Web.Tests;

public sealed class EncodingTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    private const int CCedilla = 0x00E7;

    [Theory]
    [InlineData(CCedilla)]
    [InlineData(0x00E3)]
    [InlineData(0x00C9)]
    [InlineData(0x0153)]
    [InlineData(0x2014)]
    [InlineData(0x201C)]
    [InlineData(0x2026)]
    public void HtmlEncoder_FromServices_KeepsLatinLettersAndPunctuation(int codePoint)
    {
        var text = char.ConvertFromUtf32(codePoint);

        Assert.Equal(text, HtmlEncoder().Encode(text));
    }

    [Theory]
    [InlineData(0x202E)]
    [InlineData(0x200B)]
    [InlineData(0x2066)]
    [InlineData(0x2028)]
    [InlineData(0x0430)]
    public void HtmlEncoder_FromServices_EncodesInvisibleControlsAndOtherScripts(int codePoint)
    {
        var expected = $"&#x{codePoint.ToString("X", CultureInfo.InvariantCulture)};";

        Assert.Equal(expected, HtmlEncoder().Encode(char.ConvertFromUtf32(codePoint)));
    }

    [Fact]
    public void HtmlEncoder_FromServices_EncodesMarkup()
    {
        Assert.Equal("&lt;b&gt;&amp;&quot;", HtmlEncoder().Encode("<b>&\""));
    }

    [Fact]
    public void JavaScriptAndUrlEncoders_FromServices_KeepTheirDefaults()
    {
        var text = char.ConvertFromUtf32(CCedilla);

        Assert.Equal(JavaScriptEncoder.Default.Encode(text), factory.Services.GetRequiredService<JavaScriptEncoder>().Encode(text));
        Assert.Equal(UrlEncoder.Default.Encode(text), factory.Services.GetRequiredService<UrlEncoder>().Encode(text));
        Assert.NotEqual(text, factory.Services.GetRequiredService<JavaScriptEncoder>().Encode(text));
    }

    [Fact]
    public async Task Get_Home_RendersAccentedResourceTextUnescaped()
    {
        var skipToContent = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>()["SkipToContent"].Value;
        Assert.Contains(skipToContent, character => character > 0x7F);
        using var client = HomePage.CreateClient(factory);

        var html = await client.GetStringAsync(HomePage.Url, TestContext.Current.CancellationToken);

        Assert.Contains(skipToContent, html, StringComparison.Ordinal);
    }

    private HtmlEncoder HtmlEncoder() => factory.Services.GetRequiredService<HtmlEncoder>();
}
