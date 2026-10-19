namespace Trail.Web.Pwa;

public static class PwaWebServices
{
    public static IServiceCollection AddPwaWeb(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
