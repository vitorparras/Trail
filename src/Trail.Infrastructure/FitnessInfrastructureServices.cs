using Microsoft.Extensions.DependencyInjection;

namespace Trail.Infrastructure;

public static class FitnessInfrastructureServices
{
    public static IServiceCollection AddFitnessInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
