using System.Net;
using System.Text.Json;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using Trail.Web.Htmx;

namespace Trail.Web.Tests;

public sealed class HtmxIntegrationTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    [Fact]
    public async Task Get_Home_ConfiguresHtmxBeforeLoadingItAndItsIntegration()
    {
        using var client = HomePage.CreateClient(factory);
        using var document = await HomePage.LoadAsync(client);

        var meta = document.Head!.QuerySelector("meta[name='htmx-config']");
        var htmx = document.Head.QuerySelector("script[src*='/lib/htmx/htmx.min']");
        var integration = document.Head.QuerySelector("script[src*='/js/htmx-integration']");
        Assert.NotNull(meta);
        Assert.NotNull(htmx);
        Assert.NotNull(integration);
        Assert.True(htmx.HasAttribute("defer"));
        Assert.True(integration.HasAttribute("defer"));
        Assert.True(meta.CompareDocumentPosition(htmx).HasFlag(DocumentPositions.Following));
        Assert.True(htmx.CompareDocumentPosition(integration).HasFlag(DocumentPositions.Following));

        using var configuration = JsonDocument.Parse(meta.GetAttribute("content")!);
        var root = configuration.RootElement;
        int[] noSwapStatusCodes = [204, 304];
        Assert.True(root.GetProperty("includeIndicatorCSS").GetBoolean());
        Assert.Equal(noSwapStatusCodes, root.GetProperty("noSwap").EnumerateArray().Select(code => code.GetInt32()));
    }

    [Fact]
    public async Task Get_Home_ExposesAntiforgeryTokenAndHeaderNameInMeta()
    {
        var options = factory.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
        using var client = HomePage.CreateClient(factory);
        using var document = await HomePage.LoadAsync(client);

        var meta = document.Head!.QuerySelector("meta[name='csrf-token']");

        Assert.NotNull(meta);
        Assert.Equal(HtmxIntegration.AntiforgeryHeaderName, options.HeaderName);
        Assert.Equal(options.HeaderName, meta.GetAttribute("data-header-name"));
        Assert.False(string.IsNullOrWhiteSpace(meta.GetAttribute("content")));
        Assert.Null(document.Body!.GetAttribute("hx-headers:inherited"));
    }

    [Fact]
    public async Task Post_WithTokenFromHomeMetaInConfiguredHeader_IsAccepted()
    {
        using var client = HomePage.CreateClient(factory);
        using var document = await HomePage.LoadAsync(client);
        var meta = document.Head!.QuerySelector("meta[name='csrf-token']")!;

        using var accepted = await PostLogoutAsync(client, meta.GetAttribute("data-header-name"), meta.GetAttribute("content"));
        using var rejected = await PostLogoutAsync(client, headerName: null, token: null);

        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.Equal("/", accepted.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    [Fact]
    public async Task Get_Home_VariesByHtmxRequestType()
    {
        using var client = HomePage.CreateClient(factory);

        using var response = await client.GetAsync(HomePage.Url, TestContext.Current.CancellationToken);

        Assert.Contains(HtmxIntegration.RequestTypeHeaderName, response.Headers.Vary);
    }

    [Theory]
    [InlineData("https://localhost", true)]
    [InlineData("http://localhost", false)]
    public async Task Get_Home_AntiforgeryCookieIsSecureOnlyOverHttps(string baseAddress, bool secure)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(baseAddress) });

        using var response = await client.GetAsync(HomePage.Url, TestContext.Current.CancellationToken);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
        Assert.Equal(secure, cookie.Split(';').Any(part => part.Trim().Equals("secure", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("partial", true)]
    [InlineData("full", false)]
    [InlineData(null, false)]
    public void IsHtmxPartialRequest_RequestTypeHeader_DetectsPartialRequests(string? requestType, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["HX-Request"] = "true";
        if (requestType is not null)
        {
            context.Request.Headers[HtmxIntegration.RequestTypeHeaderName] = requestType;
        }

        Assert.Equal(expected, context.Request.IsHtmxPartialRequest());
    }

    private static async Task<HttpResponseMessage> PostLogoutAsync(HttpClient client, string? headerName, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Logout");
        if (headerName is not null)
        {
            request.Headers.Add(headerName, token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
