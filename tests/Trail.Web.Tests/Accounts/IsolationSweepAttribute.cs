namespace Trail.Web.Tests.Accounts;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
internal sealed class IsolationSweepAttribute(string pageNamespace) : Attribute
{
    public string PageNamespace { get; } = pageNamespace;
}
