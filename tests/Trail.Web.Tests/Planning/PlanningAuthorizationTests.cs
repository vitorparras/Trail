using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trail.Application.Planning;
using Trail.Web.Htmx;
using Trail.Web.Tests.Accounts;

namespace Trail.Web.Tests.Planning;

public sealed class PlanningAuthorizationTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    private static readonly string PlanPath = $"/planos/{Guid.NewGuid()}";
    private static readonly string SubjectPath = $"{PlanPath}/disciplinas/{Guid.NewGuid()}";

    public static TheoryData<string, string> PlanningRoutes => new()
    {
        { "GET", "/planos" },
        { "GET", "/planos/novo" },
        { "GET", PlanPath },
        { "GET", $"{PlanPath}/editar" },
        { "GET", $"{PlanPath}?handler=SearchTopics&q=topic" },
        { "GET", SubjectPath },
        { "GET", $"{SubjectPath}?handler=TopicNote&topicId={Guid.NewGuid()}" },
        { "POST", "/planos/novo" },
        { "POST", $"{PlanPath}/editar" },
        { "POST", $"{PlanPath}?handler=AddSection" },
        { "POST", $"{SubjectPath}?handler=ChangeStatus" },
    };

    [Theory]
    [MemberData(nameof(PlanningRoutes))]
    public async Task AnonymousRequest_ToPlanningRoute_IsRedirectedToLoginWithoutReachingThePage(string method, string pathAndQuery)
    {
        using var factory = CreateFactoryWithUnreachableStore();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await SendAsync(client, method, pathAndQuery, htmx: false);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        AssertLoginRedirect(response.Headers.Location, pathAndQuery);
        Assert.Contains(HtmxIntegration.RequestTypeHeaderName, response.Headers.Vary);
    }

    [Theory]
    [MemberData(nameof(PlanningRoutes))]
    public async Task AnonymousHtmxRequest_ToPlanningRoute_AsksHtmxToNavigateToLoginReturningToThePage(string method, string pathAndQuery)
    {
        using var factory = CreateFactoryWithUnreachableStore();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await SendAsync(client, method, pathAndQuery, htmx: true);

        AssertHtmxLoginRedirect(response, pathAndQuery.Split('?')[0]);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnonymousHtmxRequest_WithCurrentUrlOfThisApp_ReturnsToTheCurrentPage()
    {
        using var factory = CreateFactoryWithUnreachableStore();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var currentPage = $"{PlanPath}?filterStatus=Studying&q=compet%C3%AAncia";

        using var response = await SendAsync(client, "POST", $"{SubjectPath}?handler=ChangeStatus", htmx: true, currentUrl: $"http://localhost{currentPage}");

        AssertHtmxLoginRedirect(response, currentPage);
    }

    [Theory]
    [InlineData("https://evil.example/planos")]
    [InlineData("http://evil.example/planos")]
    [InlineData("http://localhost:8443/planos")]
    [InlineData("https://localhost/planos")]
    [InlineData("http://localhost//evil.example/planos")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/planos")]
    [InlineData("not a url")]
    [InlineData("http://")]
    public async Task AnonymousHtmxRequest_WithForeignOrMalformedCurrentUrl_ReturnsToTheRequestedPath(string currentUrl)
    {
        using var factory = CreateFactoryWithUnreachableStore();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await SendAsync(client, "POST", $"{SubjectPath}?handler=ChangeStatus", htmx: true, currentUrl: currentUrl);

        AssertHtmxLoginRedirect(response, SubjectPath);
    }

    [Theory]
    [InlineData("/", HttpStatusCode.OK)]
    [InlineData("/Account/Login", HttpStatusCode.OK)]
    [InlineData("/Account/Register", HttpStatusCode.OK)]
    [InlineData("/Account/ForgotPassword", HttpStatusCode.OK)]
    [InlineData("/Error", HttpStatusCode.InternalServerError)]
    public async Task AnonymousRequest_ToPublicPage_IsNotChallenged(string path, HttpStatusCode expectedStatus)
    {
        using var client = baseFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    private WebApplicationFactory<Program> CreateFactoryWithUnreachableStore()
    {
        var unreachableStore = DispatchProxy.Create<IPlanningStore, FailingPlanningStore>();
        return baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPlanningStore>();
            services.AddScoped(_ => unreachableStore);
        }));
    }

    private static void AssertHtmxLoginRedirect(HttpResponseMessage response, string expectedReturnUrl)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains(HtmxIntegration.RequestTypeHeaderName, response.Headers.Vary);
        Assert.True(response.Headers.TryGetValues(HtmxIntegration.RedirectHeaderName, out var redirects));
        AssertLoginRedirect(new Uri(Assert.Single(redirects), UriKind.RelativeOrAbsolute), expectedReturnUrl);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string pathAndQuery, bool htmx, string? currentUrl = null)
    {
        var (_, _, csrfHeader, csrfToken) = await AccountTestHelper.GetDocWithAntiforgeryAsync(client, "/Account/Login");

        using var request = new HttpRequestMessage(new HttpMethod(method), pathAndQuery);
        if (method == "POST")
        {
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["name"] = "Anonymous" });
            request.Headers.Add(csrfHeader!, csrfToken);
        }

        if (htmx)
        {
            request.Headers.Add(HtmxIntegration.RequestTypeHeaderName, HtmxIntegration.PartialRequestType);
        }

        if (currentUrl is not null)
        {
            request.Headers.TryAddWithoutValidation(HtmxIntegration.CurrentUrlHeaderName, currentUrl);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static void AssertLoginRedirect(Uri? location, string expectedReturnUrl)
    {
        Assert.NotNull(location);
        var absolute = location.IsAbsoluteUri ? location : new Uri(new Uri("http://localhost"), location);
        Assert.Equal("/Account/Login", absolute.AbsolutePath);
        Assert.Equal(expectedReturnUrl, QueryHelpers.ParseQuery(absolute.Query)["ReturnUrl"].ToString());
    }
}
