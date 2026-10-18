namespace Trail.Web.Fitness;

public static class FitnessWebServices
{
    public static IServiceCollection AddFitnessWeb(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
