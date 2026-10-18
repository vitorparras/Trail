namespace Trail.Web.StudyTime;

public static class StudyTimeWebServices
{
    public static IServiceCollection AddStudyTimeWeb(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
