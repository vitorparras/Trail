using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Infrastructure.Persistence;
using Trail.Infrastructure.Persistence.Interceptors;
using Trail.Infrastructure.Persistence.PostgreSql;
using Trail.Infrastructure.Persistence.Sqlite;

namespace Trail.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(configuration);

        services.AddOptions<DatabaseOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                var rawProvider = config[$"{DatabaseOptions.SectionName}:Provider"];
                if (Enum.TryParse<DatabaseProvider>(rawProvider, ignoreCase: true, out var provider))
                {
                    options.Provider = provider;
                }

                options.ConnectionString = config.GetConnectionString("Default") ?? string.Empty;
            })
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();

        services.TryAddScoped<ICurrentUser, AnonymousCurrentUser>();
        services.AddScoped<DomainEventDispatchInterceptor>();

        services.AddDbContext<SqliteAppDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var interceptor = sp.GetRequiredService<DomainEventDispatchInterceptor>();
            var connectionString = dbOptions.Provider == DatabaseProvider.Sqlite
                ? dbOptions.ConnectionString
                : "Data Source=:memory:";

            options.UseSqlite(
                connectionString,
                builder => builder.MigrationsAssembly(typeof(SqliteAppDbContext).Assembly.GetName().Name))
                .AddInterceptors(interceptor);
        });

        services.AddDbContext<PostgreSqlAppDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var interceptor = sp.GetRequiredService<DomainEventDispatchInterceptor>();
            var connectionString = dbOptions.Provider == DatabaseProvider.PostgreSql
                ? dbOptions.ConnectionString
                : "Host=localhost;Database=dummy";

            options.UseNpgsql(
                connectionString,
                builder => builder.MigrationsAssembly(typeof(PostgreSqlAppDbContext).Assembly.GetName().Name))
                .AddInterceptors(interceptor);
        });

        services.AddScoped<AppDbContext>(sp =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return dbOptions.Provider == DatabaseProvider.PostgreSql
                ? sp.GetRequiredService<PostgreSqlAppDbContext>()
                : sp.GetRequiredService<SqliteAppDbContext>();
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<IDatabaseInitializer>(sp => sp.GetRequiredService<DatabaseInitializer>());
        services.AddHostedService(sp => sp.GetRequiredService<DatabaseInitializer>());

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

        return services;
    }

    private sealed class AnonymousCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;

        public UserId Id => default;
    }
}
