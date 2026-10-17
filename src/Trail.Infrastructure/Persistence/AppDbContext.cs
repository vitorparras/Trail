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

        builder.Entity<UserSettings>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.TimeZoneId)
                .HasMaxLength(100)
                .IsRequired();
            b.Property(x => x.PomodoroFocusMinutes)
                .IsRequired();
            b.Property(x => x.PomodoroBreakMinutes)
                .IsRequired();
            b.Property(x => x.OwnerId)
                .IsRequired();
        });

        builder.Entity<AccountInvitation>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Email)
                .HasMaxLength(256)
                .IsRequired();
            b.Property(x => x.NormalizedEmail)
                .HasMaxLength(256)
                .IsRequired();
            b.Property(x => x.Token)
                .HasMaxLength(256)
                .IsRequired();
            b.Property(x => x.InvitedByUserId)
                .IsRequired();
            b.Property(x => x.CreatedAt)
                .IsRequired();
            b.Property(x => x.ExpiresAt)
                .IsRequired();

            b.HasIndex(x => x.Token).IsUnique();
            b.HasIndex(x => x.NormalizedEmail);
        });

        builder.Entity<StudyPlan>(b =>
        {
            b.ToTable("StudyPlans");
            b.HasKey(x => x.Id);
            b.Property(x => x.OwnerId).IsRequired();
            b.HasIndex(x => x.OwnerId);
            b.Property(x => x.Name)
                .HasMaxLength(StudyPlan.MaxNameLength)
                .IsRequired();
            b.Property(x => x.Description)
                .HasMaxLength(StudyPlan.MaxDescriptionLength)
                .IsRequired();
            b.Property(x => x.ScoringRule)
                .IsRequired();
            b.Property(x => x.ExamDate);
            b.Property(x => x.DefaultMockExamQuestions)
                .IsRequired();
            b.Property(x => x.FitnessTestEnabled)
                .IsRequired();

            b.OwnsOne(x => x.DefaultPerformancePeriod, p =>
            {
                p.Property(x => x.Kind).IsRequired();
                p.Property(x => x.Days);
                p.Property(x => x.StartDate);
                p.Property(x => x.EndDate);
            });

            b.OwnsOne(x => x.PerformanceBands, pb =>
            {
                pb.Property(x => x.LowThreshold).IsRequired();
                pb.Property(x => x.HighThreshold).IsRequired();
            });

            b.HasMany(x => x.Sections)
                .WithOne()
                .HasForeignKey(x => x.StudyPlanId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Sections)
                .HasField("_sections")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            b.HasMany(x => x.Outline)
                .WithOne()
                .HasForeignKey(x => x.StudyPlanId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Outline)
                .HasField("_outline")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<Section>(b =>
        {
            b.ToTable("Sections");
            b.HasKey(x => x.Id);
            b.Property(x => x.StudyPlanId).IsRequired();
            b.Property(x => x.Name)
                .HasMaxLength(StudyPlan.MaxSectionNameLength)
                .IsRequired();
            b.Property(x => x.Order)
                .IsRequired();
        });

        builder.Entity<SubjectPlacement>(b =>
        {
            b.ToTable("SubjectPlacements");
            b.HasKey(x => new { x.StudyPlanId, x.SubjectId });
            b.Property(x => x.SectionId);
            b.Property(x => x.Order).IsRequired();
        });

        builder.Entity<Subject>(b =>
        {
            b.ToTable("Subjects");
            b.HasKey(x => x.Id);
            b.Property(x => x.OwnerId).IsRequired();
            b.HasIndex(x => x.OwnerId);
            b.Property(x => x.StudyPlanId).IsRequired();
            b.HasIndex(x => x.StudyPlanId);
            b.HasOne<StudyPlan>()
                .WithMany()
                .HasForeignKey(x => x.StudyPlanId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.Name)
                .HasMaxLength(Subject.MaxNameLength)
                .IsRequired();

            b.HasMany(x => x.Topics)
                .WithOne()
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Topics)
                .HasField("_topics")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<Topic>(b =>
        {
            b.ToTable("Topics");
            b.HasKey(x => x.Id);
            b.Property(x => x.SubjectId).IsRequired();
            b.HasIndex(x => x.SubjectId);
            b.Property(x => x.ParentId);
            b.HasIndex(x => x.ParentId);

            var topicCodeConverter = new ValueConverter<TopicCode?, string?>(
                c => c == null ? null : c.Value,
                v => string.IsNullOrEmpty(v) ? null : TopicCode.Create(v).Data);

            b.Property(x => x.Code)
                .HasConversion(topicCodeConverter)
                .HasMaxLength(100);

            b.HasIndex(x => new { x.SubjectId, x.Code }).IsUnique();

            b.Property(x => x.Title)
                .HasMaxLength(Subject.MaxTopicTitleLength)
                .IsRequired();
            b.Property(x => x.Order)
                .IsRequired();
            b.Property(x => x.Status)
                .IsRequired();
            b.Property(x => x.SearchText)
                .HasMaxLength(650)
                .IsRequired();
            b.HasIndex(x => x.SearchText);
        });

        builder.Entity<TopicNote>(b =>
        {
            b.ToTable("TopicNotes");
            b.HasKey(x => x.Id);
            b.Property(x => x.OwnerId).IsRequired();
            b.HasIndex(x => x.OwnerId);
            b.Property(x => x.TopicId).IsRequired();
            b.HasIndex(x => x.TopicId).IsUnique();
            b.HasOne<Topic>()
                .WithOne()
                .HasForeignKey<TopicNote>(x => x.TopicId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.Markdown)
                .HasMaxLength(TopicNote.MaxMarkdownLength)
                .IsRequired();
        });

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
