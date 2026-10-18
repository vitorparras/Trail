namespace Trail.Web.Practice;

public static class PracticeWebServices
{
    public static IServiceCollection AddPracticeWeb(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
