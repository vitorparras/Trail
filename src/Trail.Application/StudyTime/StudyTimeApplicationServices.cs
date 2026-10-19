using Microsoft.Extensions.DependencyInjection;

namespace Trail.Application.StudyTime;

public static class StudyTimeApplicationServices
{
    public static IServiceCollection AddStudyTimeApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
