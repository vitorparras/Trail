using Microsoft.Extensions.DependencyInjection;

namespace Trail.Application.Fitness;

public static class FitnessApplicationServices
{
    public static IServiceCollection AddFitnessApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
