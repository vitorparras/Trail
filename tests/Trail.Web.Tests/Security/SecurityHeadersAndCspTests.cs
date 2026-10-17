using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;
using Trail.Web.Security;

namespace Trail.Web.Tests.Security;

public sealed class SecurityHeadersAndCspTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    [Fact]
    public async Task RootPath_ReturnsAllRequiredSecurityHeaders()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var nosniffValues));
        Assert.Equal("nosniff", nosniffValues.Single());

        Assert.True(response.Headers.TryGetValues("X-Frame-Options", out var frameValues));
        Assert.Equal("DENY", frameValues.Single());

        Assert.True(response.Headers.TryGetValues("Referrer-Policy", out var referrerValues));
        Assert.Equal("strict-origin-when-cross-origin", referrerValues.Single());

        Assert.True(response.Headers.TryGetValues("Permissions-Policy", out var permissionsValues));
        Assert.Equal("accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()", permissionsValues.Single());

        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var cspValues));
        var csp = cspValues.Single();
        Assert.StartsWith("default-src 'self';", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none';", csp, StringComparison.Ordinal);
        Assert.Contains("base-uri 'self';", csp, StringComparison.Ordinal);
        Assert.Contains("form-action 'self';", csp, StringComparison.Ordinal);
        Assert.Contains("object-src 'none'", csp, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContentSecurityPolicy_DoesNotContainUnsafeInlineOrEval_AndMatchesScriptAndHtmxNonces()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var cspValues));
        var csp = cspValues.Single();

        Assert.DoesNotContain("unsafe-inline", csp, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unsafe-eval", csp, StringComparison.OrdinalIgnoreCase);

        var nonceMatch = Regex.Match(csp, @"'nonce-([^']+)'");
        Assert.True(nonceMatch.Success);
        var expectedNonce = nonceMatch.Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(expectedNonce));

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);

        var htmxConfigMeta = document.QuerySelector("meta[name='htmx-config']");
        Assert.NotNull(htmxConfigMeta);
        var configJson = htmxConfigMeta.GetAttribute("content");
        Assert.NotNull(configJson);

        using var configDoc = JsonDocument.Parse(configJson);
        var root = configDoc.RootElement;
        Assert.True(root.GetProperty("includeIndicatorCSS").GetBoolean());
        Assert.Equal(expectedNonce, root.GetProperty("inlineScriptNonce").GetString());
        Assert.Equal(expectedNonce, root.GetProperty("inlineStyleNonce").GetString());

        var scripts = document.Scripts.ToList();
        Assert.NotEmpty(scripts);
        Assert.All(scripts, script => Assert.Equal(expectedNonce, script.GetAttribute("nonce")));
    }

    [Fact]
    public void CspNonceTagHelper_AddsNonceToScriptAndStyleElements()
    {
        var nonceService = new CspNonceService();
        var helper = new CspNonceTagHelper(nonceService);

        var scriptContext = CreateContext("script");
        var scriptOutput = CreateOutput("script");
        helper.Process(scriptContext, scriptOutput);
        Assert.Equal(nonceService.GetNonce(), scriptOutput.Attributes["nonce"]?.Value?.ToString());

        var styleContext = CreateContext("style");
        var styleOutput = CreateOutput("style");
        helper.Process(styleContext, styleOutput);
        Assert.Equal(nonceService.GetNonce(), styleOutput.Attributes["nonce"]?.Value?.ToString());
    }

    [Fact]
    public void CspNonceTagHelper_PreservesExistingNonce()
    {
        var nonceService = new CspNonceService();
        var helper = new CspNonceTagHelper(nonceService);

        var existingAttributes = new TagHelperAttributeList { { "nonce", "existing-nonce" } };
        var scriptContext = CreateContext("script", existingAttributes);
        var scriptOutput = CreateOutput("script", existingAttributes);

        helper.Process(scriptContext, scriptOutput);

        Assert.Equal("existing-nonce", scriptOutput.Attributes["nonce"]?.Value?.ToString());
    }

    [Theory]
    [InlineData("hx-get", "/items")]
    [InlineData("hx-post", "/items")]
    [InlineData("hx-put", "/items")]
    [InlineData("hx-delete", "/items")]
    [InlineData("hx-patch", "/items")]
    [InlineData("hx-boost", "true")]
    [InlineData("data-hx-get", "/items")]
    public void CspNonceTagHelper_AddsDataHxCspToHxElements(string attributeName, string attributeValue)
    {
        var nonceService = new CspNonceService();
        var helper = new CspNonceTagHelper(nonceService);

        var attributes = new TagHelperAttributeList { { attributeName, attributeValue } };
        var context = CreateContext("button", attributes);
        var output = CreateOutput("button", attributes);

        helper.Process(context, output);

        Assert.Equal("true", output.Attributes["data-hx-csp"]?.Value?.ToString());
        Assert.False(output.Attributes.ContainsName("nonce"));
    }

    [Theory]
    [InlineData("hx-on:click", "alert(1)")]
    [InlineData("hx-on::after-request", "alert(1)")]
    [InlineData("data-hx-on:submit", "alert(1)")]
    [InlineData("hx-vals", "js:{foo: 1}")]
    [InlineData("hx-vals", "javascript:{foo: 1}")]
    public void CspNonceTagHelper_ThrowsOnForbiddenInlineScriptAttributes(string attributeName, string attributeValue)
    {
        var nonceService = new CspNonceService();
        var helper = new CspNonceTagHelper(nonceService);

        var attributes = new TagHelperAttributeList
        {
            { "hx-post", "/submit" },
            { attributeName, attributeValue },
        };
        var context = CreateContext("button", attributes);
        var output = CreateOutput("button", attributes);

        Assert.Throws<InvalidOperationException>(() => helper.Process(context, output));
    }

    [Fact]
    public async Task RepositoryOptions_DefaultSourceCodeUrlIsRenderedInFooter()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);

        var footerSourceLink = document.QuerySelector("a[data-footer-source]");
        Assert.NotNull(footerSourceLink);
        Assert.Equal("https://github.com/vitorparras/trail", footerSourceLink.GetAttribute("href"));
    }

    [Fact]
    public async Task RepositoryOptions_CustomSourceCodeUrlIsRenderedInFooter()
    {
        const string customUrl = "https://custom.example.org/trail";
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Repository:SourceCodeUrl"] = customUrl,
                });
            });
        });

        using var client = customFactory.CreateClient();
        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);

        var footerSourceLink = document.QuerySelector("a[data-footer-source]");
        Assert.NotNull(footerSourceLink);
        Assert.Equal(customUrl, footerSourceLink.GetAttribute("href"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-url")]
    [InlineData("ftp://invalid.example.com")]
    [InlineData("file:///path/to/repo")]
    public void RepositoryOptions_ValidationFailsOnInvalidUrls(string invalidUrl)
    {
        var validator = new RepositoryOptionsValidator();
        var options = new RepositoryOptions { SourceCodeUrl = invalidUrl };

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("http://example.com/repo")]
    [InlineData("https://github.com/vitorparras/trail")]
    public void RepositoryOptions_ValidationSucceedsOnValidUrls(string validUrl)
    {
        var validator = new RepositoryOptionsValidator();
        var options = new RepositoryOptions { SourceCodeUrl = validUrl };

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void RepositoryOptions_InvalidSourceCodeUrlFailsStartupValidation()
    {
        using var invalidFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Repository:SourceCodeUrl"] = "invalid-url",
                });
            });
        });

        Assert.Throws<OptionsValidationException>(() =>
        {
            using var client = invalidFactory.CreateClient();
        });
    }

    private static TagHelperContext CreateContext(string tagName, TagHelperAttributeList? attributes = null)
    {
        var list = attributes ?? [];
        return new TagHelperContext(
            tagName: tagName,
            allAttributes: list,
            items: new Dictionary<object, object>(),
            uniqueId: Guid.NewGuid().ToString("N"));
    }

    private static TagHelperOutput CreateOutput(string tagName, TagHelperAttributeList? attributes = null)
    {
        var list = attributes is not null
            ? new TagHelperAttributeList(attributes)
            : new TagHelperAttributeList();

        return new TagHelperOutput(
            tagName: tagName,
            attributes: list,
            getChildContentAsync: (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
    }
}
