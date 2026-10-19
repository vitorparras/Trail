using Trail.Application.Abstractions;
using Trail.Application.Planning;
using Trail.Web.Dashboard;
using Trail.Web.Planning;

namespace Trail.Application.Tests.Samples;

public interface ISampleExtension;

public sealed class SampleExtension : ISampleExtension;

public class OpenSampleExtension : ISampleExtension;

public abstract class SampleExtensionBase : ISampleExtension;

public sealed class DerivedSampleExtension : SampleExtensionBase;

internal sealed class InternalSampleExtension : ISampleExtension;

public sealed class GenericSampleExtension<T> : ISampleExtension;

public sealed class OutOfNamespaceSampleMetrics;

public sealed class SamplePlanActivityContributor : IPlanActivityContributor
{
    public Task<IReadOnlyList<PlanActivity>> GetLatestActivityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PlanActivity>>([]);
}

public sealed class SampleDeletionImpactContributor : IDeletionImpactContributor
{
    public Task<IReadOnlyList<DeletionImpactItem>> GetImpactAsync(DeletionScope scope, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DeletionImpactItem>>([]);
}

public sealed class SampleDashboardBlock : IDashboardBlock
{
    public int Order => 0;

    public Type ViewComponent => typeof(SampleDashboardBlock);
}

public sealed class SamplePlanLinkContributor : IPlanLinkContributor
{
    public int Order => 0;

    public PlanLink? GetLink(StudyPlanDetailView plan) => null;
}
