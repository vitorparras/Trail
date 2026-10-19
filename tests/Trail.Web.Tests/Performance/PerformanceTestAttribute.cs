using Xunit.v3;

namespace Trail.Web.Tests.Performance;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PerformanceTestAttribute : Attribute, ITraitAttribute
{
    public const string TraitName = "Category";

    public const string TraitValue = "Performance";

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new KeyValuePair<string, string>(TraitName, TraitValue)];
}
