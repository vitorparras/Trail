using Microsoft.Extensions.DependencyInjection;

namespace Trail.Application.Practice;

public static class PracticeApplicationServices
{
    public static IServiceCollection AddPracticeApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
