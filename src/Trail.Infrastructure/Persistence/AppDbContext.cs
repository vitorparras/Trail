using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Trail.Application.Abstractions;
using Trail.Domain.Accounts;
using Trail.Domain.Common;
using Trail.Domain.Planning;
using Trail.Infrastructure.Identity;

namespace Trail.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IAppDbContext
{
    private static readonly MethodInfo ConfigureTypedIdConventionMethod = typeof(AppDbContext)
        .GetMethod(nameof(ConfigureTypedIdConvention), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo ConfigureGlobalQueryFilterMethod = typeof(AppDbContext)
        .GetMethod(nameof(ConfigureGlobalQueryFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ICurrentUser _currentUser;

    public AppDbContext(DbContextOptions options, ICurrentUser currentUser)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        _currentUser = currentUser;
    }

    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    public DbSet<AccountInvitation> AccountInvitations => Set<AccountInvitation>();

    public DbSet<StudyPlan> StudyPlans => Set<StudyPlan>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<TopicNote> TopicNotes => Set<TopicNote>();

    internal DbSet<Section> Sections => Set<Section>();

    internal DbSet<SubjectPlacement> SubjectPlacements => Set<SubjectPlacement>();

    internal DbSet<Topic> Topics => Set<Topic>();

    internal UserId CurrentUserId => _currentUser.IsAuthenticated ? _currentUser.Id : default;

    internal bool IsAuthenticated => _currentUser.IsAuthenticated;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        base.ConfigureConventions(configurationBuilder);

        ConfigureTypedIds(configurationBuilder);
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (entityType.BaseType == null && typeof(IAggregateRoot).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType)
                    .Property(nameof(IAggregateRoot.Version))
                    .IsConcurrencyToken();

                ConfigureGlobalQueryFilterMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, [builder]);
            }

            foreach (var property in entityType.GetProperties())
            {
                var clrType = property.ClrType;
                var underlyingType = Nullable.GetUnderlyingType(clrType);
                var isNullable = underlyingType != null;
                var targetType = underlyingType ?? clrType;

                if (targetType.IsValueType && IsTypedId(targetType) && property.GetValueConverter() == null)
                {
                    var converter = isNullable
                        ? GetOrCreateNullableTypedIdConverter(targetType)
                        : GetOrCreateTypedIdConverter(targetType);
                    property.SetValueConverter(converter);
                }
            }
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, ValueConverter> ConverterCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, ValueConverter> NullableConverterCache = new();

    private static bool IsTypedId(Type type)
    {
        return type.GetInterfaces().Any(i =>
            i.IsGenericType &&
            i.GetGenericTypeDefinition() == typeof(ITypedId<>) &&
            i.GetGenericArguments()[0] == type);
    }

    private static void ConfigureTypedIds(ModelConfigurationBuilder configurationBuilder)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var typedIdTypes = new HashSet<Type>();

        foreach (var assembly in assemblies)
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type is { IsValueType: true, IsEnum: false } && IsTypedId(type))
                    {
                        typedIdTypes.Add(type);
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var type in ex.Types)
                {
                    if (type is { IsValueType: true, IsEnum: false } && IsTypedId(type))
                    {
                        typedIdTypes.Add(type);
                    }
                }
            }
            catch
            {
            }
        }

        foreach (var typedIdType in typedIdTypes)
        {
            ConfigureTypedIdConventionMethod.MakeGenericMethod(typedIdType).Invoke(null, [configurationBuilder]);
        }
    }

    private static void ConfigureTypedIdConvention<TId>(ModelConfigurationBuilder configurationBuilder)
        where TId : struct, ITypedId<TId>
    {
        configurationBuilder.Properties<TId>().HaveConversion<TypedIdConverter<TId>>();
        configurationBuilder.Properties<TId?>().HaveConversion<NullableTypedIdConverter<TId>>();
    }

    private void ConfigureGlobalQueryFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, IAggregateRoot
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity => IsAuthenticated && entity.OwnerId == CurrentUserId);
    }

    private static TId CreateTypedId<TId>(Guid value)
        where TId : struct, ITypedId<TId> => TId.From(value);

    internal static ValueConverter GetOrCreateTypedIdConverter(Type typedIdType)
    {
        return ConverterCache.GetOrAdd(typedIdType, static type =>
        {
            var converterType = typeof(TypedIdConverter<>).MakeGenericType(type);
            return (ValueConverter)Activator.CreateInstance(converterType, nonPublic: true)!;
        });
    }

    internal static ValueConverter GetOrCreateNullableTypedIdConverter(Type typedIdType)
    {
        return NullableConverterCache.GetOrAdd(typedIdType, static type =>
        {
            var converterType = typeof(NullableTypedIdConverter<>).MakeGenericType(type);
            return (ValueConverter)Activator.CreateInstance(converterType, nonPublic: true)!;
        });
    }

    internal sealed class TypedIdConverter<TId>() : ValueConverter<TId, Guid>(
        id => id.Value,
        value => CreateTypedId<TId>(value))
        where TId : struct, ITypedId<TId>;

    internal sealed class NullableTypedIdConverter<TId>() : ValueConverter<TId?, Guid?>(
        id => id.HasValue ? id.Value.Value : null,
        value => value.HasValue ? CreateTypedId<TId>(value.Value) : null)
        where TId : struct, ITypedId<TId>;

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : (v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc)),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : (v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))) : null,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null);
}
