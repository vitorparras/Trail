using System.Reflection;

namespace Trail.Web.Tests.Planning;

public class FailingPlanningStore : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new InvalidOperationException($"The planning store must not be reached ({targetMethod?.Name}).");
}
