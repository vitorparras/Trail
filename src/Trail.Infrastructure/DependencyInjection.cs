using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Trail.Application;
using Trail.Application.Abstractions;
using Trail.Application.Accounts;
using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Infrastructure.Email;
using Trail.Infrastructure.Identity;
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
        services.TryAddSingleton(TimeProvider.System);

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

        services.AddOptions<RegistrationOptions>()
            .BindConfiguration(RegistrationOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RegistrationOptions>, RegistrationOptionsValidator>();

        services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();

        services.AddOptions<DataProtectionPersistenceOptions>()
            .BindConfiguration(DataProtectionPersistenceOptions.SectionName)
            .ValidateOnStart();

        var dataProtectionSection = configuration.GetSection(DataProtectionPersistenceOptions.SectionName);
        var rawKeysPath = dataProtectionSection[nameof(DataProtectionPersistenceOptions.KeysPath)] ?? "/data/keys";
        var keysDirectory = ResolveKeysDirectory(rawKeysPath);

        services.AddDataProtection()
            .SetApplicationName("Trail")
            .PersistKeysToFileSystem(keysDirectory);

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

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
        })
        .AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Forbidden";
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            options.Events.OnValidatePrincipal = async context =>
            {
                var signInManager = context.HttpContext.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
                var user = await signInManager.ValidateSecurityStampAsync(context.Principal);
                if (user is null || !user.IsActive)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            };
        });

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;

            options.Password.RequiredLength = 6;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddSignInManager<SignInManager<ApplicationUser>>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(2);
        });

        services.Replace(ServiceDescriptor.Scoped<IAccountIdentityGateway, AccountIdentityGateway>());
        services.Replace(ServiceDescriptor.Scoped<IEmailSender, MailKitEmailSender>());
        services.Replace(ServiceDescriptor.Scoped<IUserSettingsStore, EfUserSettingsStore>());
        services.Replace(ServiceDescriptor.Scoped<IPlanningStore, EfPlanningStore>());

        services.AddContributorsFrom(typeof(DependencyInjection).Assembly);
        services.AddPracticeInfrastructure();
        services.AddFitnessInfrastructure();
        services.AddStudyTimeInfrastructure();

        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<IDatabaseInitializer>(sp => sp.GetRequiredService<DatabaseInitializer>());
        services.AddHostedService(sp => sp.GetRequiredService<DatabaseInitializer>());

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

        return services;
    }

    private static DirectoryInfo ResolveKeysDirectory(string configuredPath)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && configuredPath.StartsWith("/data", StringComparison.OrdinalIgnoreCase))
            {
                var fallbackDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Trail", "keys");
                var dir = new DirectoryInfo(fallbackDirectory);
                if (!dir.Exists)
                {
                    dir.Create();
                }

                return dir;
            }

            var targetPath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(AppContext.BaseDirectory, configuredPath);

            var targetDir = new DirectoryInfo(targetPath);
            if (!targetDir.Exists)
            {
                targetDir.Create();
            }

            return targetDir;
        }
        catch
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "Trail", "keys");
            var fallbackDir = new DirectoryInfo(tempDirectory);
            if (!fallbackDir.Exists)
            {
                fallbackDir.Create();
            }

            return fallbackDir;
        }
    }

    private sealed class AnonymousCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;

        public UserId Id => default;
    }
}
