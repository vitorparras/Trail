using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenTelemetry.Trace;
using Trail.Domain.Common;
using Trail.Infrastructure.Identity;
using Trail.Infrastructure.Persistence;
using Trail.Web.Tests.Accounts;
using Trail.Web.Tests.Observability;

namespace Trail.Web.Tests.Performance;

public sealed class PerformanceEnvironment : IAsyncDisposable
{
    private const string OwnerEmail = "performance@trail.local";
    private const string OwnerPassword = "PerformancePass123!";

    private readonly TrailWebApplicationFactory _baseFactory;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly ServerSpanRecorder _serverSpans;

    private PerformanceEnvironment(
        TrailWebApplicationFactory baseFactory,
        WebApplicationFactory<Program> factory,
        HttpClient client,
        ServerSpanRecorder serverSpans,
        PerformanceSeedContext seedContext)
    {
        _baseFactory = baseFactory;
        _factory = factory;
        _client = client;
        _serverSpans = serverSpans;
        SeedContext = seedContext;
    }

    public DatabaseProvider Provider => SeedContext.Provider;

    public UserId OwnerId => SeedContext.OwnerId;

    public SeededPerformancePlan Plan => SeedContext.Plan;

    public IServiceProvider Services => _factory.Services;

    public PerformanceSeedContext SeedContext { get; }

    public static async Task<PerformanceEnvironment> CreateAsync(
        DatabaseProvider provider,
        PlanShape shape,
        Action<IServiceCollection>? configureServices = null,
        Func<PerformanceSeedContext, Task>? seed = null)
    {
        ArgumentNullException.ThrowIfNull(shape);

        var serverSpans = new ServerSpanRecorder();
        var baseFactory = TrailWebApplicationFactory.Create(provider, new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = string.Empty,
            ["Registration:Mode"] = "Open",
        });

        try
        {
            var factory = baseFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddOpenTelemetry().WithTracing(tracing => tracing.AddProcessor(serverSpans));
                    configureServices?.Invoke(services);
                });
            });

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            });

            var ownerId = await RegisterAndSignInAsync(factory, client);
            var seedContext = new PerformanceSeedContext(factory.Services, provider, ownerId);
            seedContext.Plan = await PerformancePlanSeeder.SeedAsync(seedContext, shape, "Performance plan");

            if (seed is not null)
            {
                await seed(seedContext);
            }

            return new PerformanceEnvironment(baseFactory, factory, client, serverSpans, seedContext);
        }
        catch
        {
            await baseFactory.DisposeAsync();
            throw;
        }
    }

    public async Task<PerformanceMeasurement> MeasureAsync(PerformanceScenario scenario, PerformanceBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        budget ??= PerformanceBudget.Default;
        var path = scenario.Path(Plan);
        var expectedTexts = scenario.ExpectedTexts?.Invoke(Plan) ?? [];

        for (var warmup = 0; warmup < budget.WarmupRequests; warmup++)
        {
            using var request = scenario.CreateRequest(Plan);
            using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            EnsureRendered(scenario, path, response.StatusCode, body, expectedTexts);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var samples = new List<TimeSpan>(budget.SampledRequests);
        for (var sample = 0; sample < budget.SampledRequests; sample++)
        {
            using var request = scenario.CreateRequest(Plan);
            var trace = _serverSpans.Trace(request);
            using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            EnsureRendered(scenario, path, response.StatusCode, body, expectedTexts);
            var serverSpan = await trace.ServerSpanEnded;
            samples.Add(serverSpan.Duration);
        }

        return new PerformanceMeasurement(scenario.Name, Provider, path, samples, budget);
    }

    public async Task AssertWithinBudgetAsync(PerformanceScenario scenario, PerformanceBudget? budget = null)
    {
        var measurement = await MeasureAsync(scenario, budget);
        PerformanceReport.Publish(measurement);

        if (!measurement.IsWithinBudget)
        {
            Assert.Fail(measurement.Describe());
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _baseFactory.DisposeAsync();
    }

    private static void EnsureRendered(
        PerformanceScenario scenario,
        string path,
        HttpStatusCode status,
        string body,
        IReadOnlyList<string> expectedTexts)
    {
        if (status != HttpStatusCode.OK)
        {
            Assert.Fail($"Scenario '{scenario.Name}' (GET {path}) answered {(int)status} instead of 200, so its timing says nothing about the page.");
        }

        foreach (var expectedText in expectedTexts)
        {
            if (!body.Contains(expectedText, StringComparison.Ordinal))
            {
                Assert.Fail($"Scenario '{scenario.Name}' (GET {path}) did not render the seeded data ('{expectedText}' is missing), so its timing says nothing about the page.");
            }
        }
    }

    private static async Task<UserId> RegisterAndSignInAsync(WebApplicationFactory<Program> factory, HttpClient client)
    {
        using var registerResponse = await AccountTestHelper.RegisterAsync(client, OwnerEmail, OwnerPassword);
        Assert.True(registerResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);

        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(OwnerEmail);
            Assert.NotNull(user);

            if (!user.EmailConfirmed)
            {
                var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                Assert.True((await userManager.ConfirmEmailAsync(user, token)).Succeeded);
            }

            userId = user.Id;
        }

        using var signInResponse = await AccountTestHelper.SignInAsync(client, OwnerEmail, OwnerPassword);
        Assert.Equal(HttpStatusCode.Redirect, signInResponse.StatusCode);

        return UserId.From(userId);
    }
}
