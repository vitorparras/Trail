using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trail.Application.Abstractions;
using Trail.Application.Planning;
using Trail.Web.Tests.Accounts;

namespace Trail.Web.Tests.Planning;

internal static class PlanningTestHelper
{
    public static WebApplicationFactory<Program> CreateTestFactory(
        TrailWebApplicationFactory baseFactory,
        IPlanningStore store,
        TestCurrentUser currentUser)
    {
        return baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICurrentUser>();
                services.AddScoped<ICurrentUser>(_ => currentUser);

                services.RemoveAll<IPlanningStore>();
                services.AddScoped(_ => store);

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestCurrentUserAuthenticationHandler>(TestCurrentUserAuthenticationHandler.SchemeName, null);
                services.Configure<AuthenticationOptions>(options =>
                    options.DefaultAuthenticateScheme = TestCurrentUserAuthenticationHandler.SchemeName);
            });
        });
    }

    public static async Task<(HttpResponseMessage Response, IHtmlDocument Document, string? CsrfHeader, string? CsrfToken)> GetDocWithAntiforgeryAsync(
        HttpClient client,
        string url)
    {
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);

        var meta = document.Head?.QuerySelector("meta[name='csrf-token']");
        var header = meta?.GetAttribute("data-header-name") ?? "X-CSRF-TOKEN";
        var token = meta?.GetAttribute("content");

        return (response, document, header, token);
    }

    public static async Task<HttpResponseMessage> PostFormWithAntiforgeryAsync(
        HttpClient client,
        string url,
        Dictionary<string, string> formData,
        string? csrfHeader = null,
        string? csrfToken = null,
        Dictionary<string, string>? customHeaders = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(formData)
        };

        if (!string.IsNullOrWhiteSpace(csrfHeader) && !string.IsNullOrWhiteSpace(csrfToken))
        {
            request.Headers.Add(csrfHeader, csrfToken);
        }

        if (customHeaders != null)
        {
            foreach (var (header, value) in customHeaders)
            {
                request.Headers.Add(header, value);
            }
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
