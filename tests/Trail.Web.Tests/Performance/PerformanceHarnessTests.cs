using System.Reflection;
using Trail.Infrastructure.Persistence;

namespace Trail.Web.Tests.Performance;

public sealed class PerformanceHarnessTests
{
    [Fact]
    public void PlanShape_Default_DistributesTheThousandTopicsAcrossAllSubjects()
    {
        var counts = PlanShape.Default.TopicsPerSubject();

        Assert.Equal(PlanShape.Default.Subjects, counts.Count);
        Assert.Equal(PlanShape.Default.Topics, counts.Sum());
        Assert.All(counts, count => Assert.True(count > 0));
        Assert.Equal(counts.Max(), counts[0]);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(20, 10, 19)]
    [InlineData(10, 5, 10)]
    public void Measurement_Percentiles_UseNearestRankOverTheSortedSamples(int count, int expectedP50Rank, int expectedP95Rank)
    {
        var samples = Enumerable.Range(1, count).Reverse().Select(value => TimeSpan.FromMilliseconds(value * 10)).ToList();

        var measurement = new PerformanceMeasurement("scenario", DatabaseProvider.Sqlite, "/", samples, PerformanceBudget.Default);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedP50Rank * 10), measurement.P50);
        Assert.Equal(TimeSpan.FromMilliseconds(expectedP95Rank * 10), measurement.P95);
        Assert.Equal(TimeSpan.FromMilliseconds(count * 10), measurement.Max);
        Assert.Equal(TimeSpan.FromMilliseconds(10), measurement.Min);
    }

    [Fact]
    public void Measurement_P95AboveTheBudget_IsOutsideTheBudgetAndReportsEveryFigure()
    {
        var samples = Enumerable.Range(1, 20).Select(value => TimeSpan.FromMilliseconds(value == 20 || value == 19 ? 400 : 100)).ToList();

        var measurement = new PerformanceMeasurement("Plan page", DatabaseProvider.PostgreSql, "/planos/1", samples, PerformanceBudget.Default);
        var description = measurement.Describe();

        Assert.False(measurement.IsWithinBudget);
        Assert.Contains("OVER budget", description, StringComparison.Ordinal);
        Assert.Contains("Plan page", description, StringComparison.Ordinal);
        Assert.Contains("PostgreSql", description, StringComparison.Ordinal);
        Assert.Contains("p50=100.0 ms", description, StringComparison.Ordinal);
        Assert.Contains("p95=400.0 ms", description, StringComparison.Ordinal);
        Assert.Contains("max=400.0 ms", description, StringComparison.Ordinal);
    }

    [Fact]
    public void Measurement_P95AtTheBudget_IsWithinTheBudget()
    {
        var samples = Enumerable.Repeat(PerformanceBudget.Default.ServerTime, 20).ToList();

        var measurement = new PerformanceMeasurement("scenario", DatabaseProvider.Sqlite, "/", samples, PerformanceBudget.Default);

        Assert.True(measurement.IsWithinBudget);
    }

    [Fact]
    public void PerformanceTestClasses_AreTaggedAndShareTheSequentialCollection()
    {
        var tagged = typeof(PerformanceHarnessTests).Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttribute<PerformanceTestAttribute>() is not null)
            .ToList();

        Assert.NotEmpty(tagged);
        Assert.All(tagged, type =>
        {
            var collection = type.GetCustomAttribute<CollectionAttribute>();
            Assert.True(
                collection?.Name == PerformanceCollections.Sequential,
                $"{type.FullName} must be in the '{PerformanceCollections.Sequential}' collection so that performance tests never run in parallel.");
        });
    }

    [Fact]
    public void PerformanceTestAttribute_ExposesTheCategoryTraitUsedByTheFilters()
    {
        var trait = Assert.Single(new PerformanceTestAttribute().GetTraits());

        Assert.Equal("Category", trait.Key);
        Assert.Equal("Performance", trait.Value);
    }
}
