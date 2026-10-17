using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Trail.Web.Tests;

internal static class HomePage
{
    public static Uri Url { get; } = new("/", UriKind.Relative);

    public static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static async Task<IHtmlDocument> LoadAsync(HttpClient client, Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url);
        configure?.Invoke(request);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return await ParseAsync(response);
    }

    public static async Task<IHtmlDocument> ParseAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return new HtmlParser().ParseDocument(html);
    }
}
