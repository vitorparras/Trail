using System.Net;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Localization;

namespace Trail.Web.Tests.Results;

public sealed class ErrorHandlingTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    private readonly IStringLocalizer<SharedResource> _localizer = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

    [Fact]
    public async Task Get_Error_WithoutHtmx_Returns500WithFullPageAndTrackingCode()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/Error", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = new HtmlParser().ParseDocument(html);

        Assert.NotNull(doc.QuerySelector("html"));
        Assert.NotNull(doc.QuerySelector("header"));
        Assert.NotNull(doc.QuerySelector("main"));
        Assert.Equal(_localizer["ErrorTitle"].Value, doc.QuerySelector("main h1")?.TextContent.Trim());
        Assert.Equal(_localizer["UnexpectedError"].Value, doc.QuerySelector("main p")?.TextContent.Trim());

        var trackingParagraph = doc.QuerySelector("main p.font-mono");
        Assert.NotNull(trackingParagraph);
        Assert.Contains("Código de rastreio:", trackingParagraph.TextContent);
    }

    [Fact]
    public async Task Get_Error_WithHtmx_Returns500WithPartialAndTrackingCode()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Error");
        request.Headers.Add("HX-Request-Type", "partial");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var doc = new HtmlParser().ParseDocument(html);

        Assert.Null(doc.QuerySelector("header"));
        Assert.Equal(_localizer["ErrorTitle"].Value, doc.QuerySelector("h1")?.TextContent.Trim());
        Assert.Equal(_localizer["UnexpectedError"].Value, doc.QuerySelector("p")?.TextContent.Trim());

        var trackingParagraph = doc.QuerySelector("p.font-mono");
        Assert.NotNull(trackingParagraph);
        Assert.Contains("Código de rastreio:", trackingParagraph.TextContent);
    }

    [Fact]
    public async Task Post_Error_Returns500InternalServerError()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/Error", new StringContent(string.Empty), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
