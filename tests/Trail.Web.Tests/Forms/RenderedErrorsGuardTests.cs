using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Localization;

namespace Trail.Web.Tests.Forms;

public sealed class RenderedErrorsGuardTests(TrailWebApplicationFactory baseFactory) : IClassFixture<TrailWebApplicationFactory>
{
    private readonly IStringLocalizer<SharedResource> _localizer = baseFactory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

    [Fact]
    public async Task Guard_WhenAnErrorIsNotRenderedInAnHtmlResponse_Throws()
    {
        using var client = CreateClient("Injected error that no page renders");

        var failure = await Assert.ThrowsAsync<UnrenderedModelStateErrorException>(
            () => client.GetAsync("/Account/Login", TestContext.Current.CancellationToken));

        Assert.Equal(["Injected error that no page renders"], failure.Messages);
    }

    [Fact]
    public async Task Guard_WhenTheTextOnlyAppearsInAnAttribute_Throws()
    {
        var placeholder = _localizer["EmailPlaceholder"].Value;
        using var client = CreateClient(placeholder);

        var failure = await Assert.ThrowsAsync<UnrenderedModelStateErrorException>(
            () => client.GetAsync("/Account/Login", TestContext.Current.CancellationToken));

        Assert.Equal([placeholder], failure.Messages);
    }

    [Fact]
    public async Task Guard_WhenTheResponseIsARedirectAndAnErrorWasRecorded_Throws()
    {
        using var client = CreateClient("Error dropped by a redirect");

        await Assert.ThrowsAsync<UnrenderedModelStateErrorException>(
            () => client.GetAsync("/Account/Logout", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Guard_WhenTheErrorIsRendered_LetsTheResponseThrough()
    {
        var heading = _localizer["LoginHeading"].Value;
        using var client = CreateClient(heading);

        using var response = await client.GetAsync("/Account/Login", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient CreateClient(string injectedError) =>
        baseFactory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.Configure<MvcOptions>(options => options.Filters.Add(new InjectErrorFilter(injectedError)))))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private sealed class InjectErrorFilter(string message) : IAsyncPageFilter
    {
        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        {
            context.ModelState.AddModelError("Injected", message);
            return Task.CompletedTask;
        }

        public Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next) =>
            next();
    }
}
