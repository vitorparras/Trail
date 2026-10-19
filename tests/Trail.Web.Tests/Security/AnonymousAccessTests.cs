using System.Net;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.WebUtilities;
using Trail.Web.Htmx;
using Trail.Web.Tests.Accounts;

namespace Trail.Web.Tests.Security;

public sealed class AnonymousAccessTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    private const string LoginPath = "/Account/Login";

    private static readonly Dictionary<string, HttpStatusCode> PublicPages = new(StringComparer.Ordinal)
    {
        ["/Index"] = HttpStatusCode.OK,
        ["/Error"] = HttpStatusCode.InternalServerError,
        ["/Account/AcceptInvite"] = HttpStatusCode.Redirect,
        ["/Account/ConfirmEmail"] = HttpStatusCode.UnprocessableEntity,
        ["/Account/ForgotPassword"] = HttpStatusCode.OK,
        ["/Account/Login"] = HttpStatusCode.OK,
        ["/Account/Logout"] = HttpStatusCode.Redirect,
        ["/Account/Register"] = HttpStatusCode.OK,
        ["/Account/ResendConfirmation"] = HttpStatusCode.OK,
        ["/Account/ResetPassword"] = HttpStatusCode.OK,
    };

    private static readonly HashSet<string> PrivatePages = new(StringComparer.Ordinal)
    {
        "/Account/Settings",
    };

    private static readonly string[] PrivateFolders = ["/Admin/", "/Plans/", "/Practice/", "/Fitness/", "/StudyTime/"];

    private static readonly string[] UnknownUrls = ["/favicon.ico", "/robots.txt", "/apple-touch-icon.png", "/css/missing.css", "/health", "/does-not-exist"];

    [Fact]
    public void PublicAndPrivatePageLists_NameOnlyDiscoveredPages()
    {
        var discovered = DiscoverPageRoutes().Select(route => route.Page).ToHashSet(StringComparer.Ordinal);

        Assert.All(PublicPages.Keys, page => Assert.Contains(page, discovered));
        Assert.All(PrivatePages, page => Assert.Contains(page, discovered));
        Assert.Contains("/Admin/Users", discovered);
        Assert.Contains("/Plans/Index", discovered);
    }

    [Fact]
    public void DiscoveredPages_AreEachClassifiedAsEitherPublicOrPrivate()
    {
        var unclassified = DiscoverPageRoutes()
            .Select(route => route.Page)
            .Distinct(StringComparer.Ordinal)
            .Where(page => PublicPages.ContainsKey(page) == IsPrivate(page))
            .ToList();

        Assert.True(
            unclassified.Count == 0,
            "Classify each page as exactly one of public (PublicPages, with [AllowAnonymous]) or private (PrivatePages or PrivateFolders): " + string.Join(", ", unclassified));
    }

    [Fact]
    public async Task AnonymousGet_ToEveryPage_AnswersOnPublicPagesAndLeadsToLoginOnPrivatePages()
    {
        using var client = CreateClient();
        var failures = new List<string>();

        foreach (var route in DiscoverPageRoutes().Where(route => PublicPages.ContainsKey(route.Page) || IsPrivate(route.Page)))
        {
            using var response = await client.GetAsync(route.Path, TestContext.Current.CancellationToken);

            var failure = PublicPages.TryGetValue(route.Page, out var expectedStatus)
                ? DescribePublicFailure(response, expectedStatus)
                : DescribeLoginRedirectFailure(response, route.Path);

            if (failure is not null)
            {
                failures.Add($"{route.Page} ({route.Path}): {failure}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task AnonymousHtmxGet_ToEveryPrivatePage_AsksForAFullPageNavigationToLogin()
    {
        using var client = CreateClient();
        var failures = new List<string>();

        foreach (var route in DiscoverPageRoutes().Where(route => IsPrivate(route.Page)))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, route.Path);
            request.Headers.Add(HtmxIntegration.RequestTypeHeaderName, HtmxIntegration.PartialRequestType);

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            var failure = DescribeHtmxLoginRedirectFailure(response, body, route.Path);
            if (failure is not null)
            {
                failures.Add($"{route.Page} ({route.Path}): {failure}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task AnonymousGet_ToHealthCheck_AnswersHealthyWithoutLogin(string path)
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnonymousGet_ToStaticFilesOfTheLoginPage_AnswersWithoutLogin()
    {
        using var client = CreateClient();
        using var loginResponse = await client.GetAsync(LoginPath, TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(await loginResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var urls = document.Scripts.Select(script => script.Source)
            .Concat(document.QuerySelectorAll("link[rel='stylesheet']").Select(link => link.GetAttribute("href")))
            .Concat(document.QuerySelectorAll("use").Select(use => use.GetAttribute("href")?.Split('#')[0]))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Contains(urls, url => url.StartsWith("/css/app.", StringComparison.Ordinal));
        foreach (var url in urls)
        {
            using var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {(int)response.StatusCode}");
            Assert.True(response.Content.Headers.ContentLength > 0, url);
        }
    }

    [Fact]
    public async Task AnonymousGet_ToAnUnknownUrl_AnswersNotFoundWithoutConsumingTheLoginRateLimit()
    {
        using var limitedFactory = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Security:AuthRateLimitPermitLimit"] = "2" })));
        using var browserLikeClient = limitedFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true, HandleCookies = false });
        var failures = new List<string>();

        foreach (var url in UnknownUrls)
        {
            foreach (var htmx in new[] { false, true })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (htmx)
                {
                    request.Headers.Add(HtmxIntegration.RequestTypeHeaderName, HtmxIntegration.PartialRequestType);
                }

                using var response = await browserLikeClient.SendAsync(request, TestContext.Current.CancellationToken);
                if (response.StatusCode != HttpStatusCode.NotFound
                    || response.RequestMessage?.RequestUri?.AbsolutePath != url
                    || response.Headers.Contains(HtmxIntegration.RedirectHeaderName))
                {
                    failures.Add($"{url} (HTMX: {htmx}): {(int)response.StatusCode} at {response.RequestMessage?.RequestUri}");
                }
            }
        }

        HttpStatusCode[] loginViews =
        [
            await GetStatusAsync(browserLikeClient, LoginPath),
            await GetStatusAsync(browserLikeClient, LoginPath),
            await GetStatusAsync(browserLikeClient, LoginPath),
        ];

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], loginViews);
    }

    [Theory]
    [InlineData("/favicon.ico")]
    [InlineData("/does-not-exist")]
    public async Task AuthenticatedGet_ToAnUnknownUrl_AnswersNotFound(string url)
    {
        using var authenticatedFactory = AccountTestHelper.CreateTestFactory(
            factory,
            new TestAccountIdentityGateway(),
            new TestEmailSender(),
            new TestUserSettingsStore(),
            new TestCurrentUser { IsAuthenticated = true, IsAdmin = true });
        using var client = authenticatedFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(client, "/Admin/Users"));
        Assert.Equal(HttpStatusCode.NotFound, await GetStatusAsync(client, url));
    }

    private static async Task<HttpStatusCode> GetStatusAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    private static bool IsPrivate(string page) =>
        PrivatePages.Contains(page) || PrivateFolders.Any(folder => page.StartsWith(folder, StringComparison.Ordinal));

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private List<PageRoute> DiscoverPageRoutes() =>
        [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ISuppressMatchingMetadata>()?.SuppressMatching != true)
            .Select(endpoint => (Page: endpoint.Metadata.GetMetadata<PageActionDescriptor>()?.ViewEnginePath, endpoint.RoutePattern))
            .Where(route => route.Page is not null)
            .Select(route => new PageRoute(route.Page!, PathFor(route.RoutePattern)))
            .DistinctBy(route => route.Path, StringComparer.Ordinal)
            .OrderBy(route => route.Path, StringComparer.Ordinal)];

    private static string PathFor(RoutePattern pattern) =>
        "/" + string.Join('/', pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternSeparatorPart separator => separator.Content,
            RoutePatternParameterPart parameter => SampleValue(parameter, pattern),
            _ => throw new InvalidOperationException($"Unsupported route part in '{pattern.RawText}'."),
        }))));

    private static string SampleValue(RoutePatternParameterPart parameter, RoutePattern pattern) =>
        parameter.ParameterPolicies.Count > 0 && parameter.ParameterPolicies.All(policy => policy.Content == "guid")
            ? Guid.NewGuid().ToString()
            : throw new InvalidOperationException($"Add a sample value for the route parameter '{parameter.Name}' of '{pattern.RawText}'.");

    private static string? DescribePublicFailure(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        if (response.StatusCode != expectedStatus)
        {
            return $"expected {(int)expectedStatus}, got {(int)response.StatusCode}";
        }

        return IsLogin(response.Headers.Location) ? "redirected to login" : null;
    }

    private static string? DescribeLoginRedirectFailure(HttpResponseMessage response, string path)
    {
        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            return $"expected a redirect to login, got {(int)response.StatusCode}";
        }

        return DescribeLoginUrlFailure(response.Headers.Location, path);
    }

    private static string? DescribeHtmxLoginRedirectFailure(HttpResponseMessage response, string body, string path)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return $"expected 401 with {HtmxIntegration.RedirectHeaderName}, got {(int)response.StatusCode}";
        }

        if (response.Headers.Location is not null || body.Length > 0)
        {
            return "expected no Location header and an empty body";
        }

        return response.Headers.TryGetValues(HtmxIntegration.RedirectHeaderName, out var redirects)
            ? DescribeLoginUrlFailure(new Uri(redirects.Single(), UriKind.RelativeOrAbsolute), path)
            : $"missing {HtmxIntegration.RedirectHeaderName}";
    }

    private static string? DescribeLoginUrlFailure(Uri? location, string path)
    {
        if (!IsLogin(location))
        {
            return $"expected the login page, got {location}";
        }

        var returnUrl = QueryHelpers.ParseQuery(Absolute(location!).Query)["ReturnUrl"].ToString();
        return returnUrl == path ? null : $"expected ReturnUrl {path}, got {returnUrl}";
    }

    private static bool IsLogin(Uri? location) =>
        location is not null && Absolute(location).AbsolutePath == LoginPath;

    private static Uri Absolute(Uri location) =>
        location.IsAbsoluteUri ? location : new Uri(new Uri("http://localhost"), location);

    private sealed record PageRoute(string Page, string Path);
}
