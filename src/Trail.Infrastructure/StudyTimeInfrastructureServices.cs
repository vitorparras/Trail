using Microsoft.Extensions.DependencyInjection;

namespace Trail.Infrastructure;

public static class StudyTimeInfrastructureServices
{
    public static IServiceCollection AddStudyTimeInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
