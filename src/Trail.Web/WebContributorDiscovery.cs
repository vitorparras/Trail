using System.Reflection;
using Trail.Application;
using Trail.Web.Dashboard;
using Trail.Web.Planning;

namespace Trail.Web;

public static class WebContributorDiscovery
{
    public static IServiceCollection AddWebContributorsFrom(this IServiceCollection services, Assembly assembly) =>
        services
            .AddImplementationsFrom<IDashboardBlock>(assembly, ServiceLifetime.Scoped)
            .AddImplementationsFrom<IPlanLinkContributor>(assembly, ServiceLifetime.Scoped);
}
