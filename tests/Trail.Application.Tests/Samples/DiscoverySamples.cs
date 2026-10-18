using Trail.Application.Abstractions;
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

public sealed class SamplePlanActivityContributor : IPlanActivityContributor;

public sealed class SampleDeletionImpactContributor : IDeletionImpactContributor;

public sealed class SampleDashboardBlock : IDashboardBlock;

public sealed class SamplePlanLinkContributor : IPlanLinkContributor;
