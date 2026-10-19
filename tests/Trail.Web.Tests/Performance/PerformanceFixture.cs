using Trail.Infrastructure.Persistence;

namespace Trail.Web.Tests.Performance;

public abstract class PerformanceFixture : IAsyncDisposable
{
    private readonly Dictionary<DatabaseProvider, PerformanceEnvironment> _environments = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    protected virtual PlanShape Shape => PlanShape.Default;

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    protected virtual Task SeedAsync(PerformanceSeedContext context) => Task.CompletedTask;

    public async Task<PerformanceEnvironment> GetEnvironmentAsync(DatabaseProvider provider)
    {
        await _gate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            if (!_environments.TryGetValue(provider, out var environment))
            {
                environment = await PerformanceEnvironment.CreateAsync(provider, Shape, ConfigureServices, SeedAsync);
                _environments[provider] = environment;
            }

            return environment;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AssertWithinBudgetAsync(
        DatabaseProvider provider,
        PerformanceScenario scenario,
        PerformanceBudget? budget = null)
    {
        var environment = await GetEnvironmentAsync(provider);
        await environment.AssertWithinBudgetAsync(scenario, budget);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var environment in _environments.Values)
        {
            await environment.DisposeAsync();
        }

        _gate.Dispose();
        GC.SuppressFinalize(this);
    }
}
