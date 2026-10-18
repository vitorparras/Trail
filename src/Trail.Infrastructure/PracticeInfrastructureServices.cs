using Microsoft.Extensions.DependencyInjection;

namespace Trail.Infrastructure;

public static class PracticeInfrastructureServices
{
    public static IServiceCollection AddPracticeInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
