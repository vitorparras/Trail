using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Practice;

namespace Trail.Infrastructure.Persistence.Configurations.Practice;

public static class QuestionCountMapping
{
    public const string CheckConstraintSql =
        "\"Total\" > 0 AND \"Correct\" >= 0 AND \"Wrong\" >= 0 AND \"Correct\" + \"Wrong\" <= \"Total\"";

    private static readonly string[] ReservedColumns =
    [
        nameof(QuestionCount.Total),
        nameof(QuestionCount.Correct),
        nameof(QuestionCount.Wrong),
    ];

    public static EntityTypeBuilder<TEntity> HasQuestionCount<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, QuestionCount?>> property,
        string checkConstraintName)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkConstraintName);

        EnsureNoConflict(builder.Metadata);

        builder.ComplexProperty(property, count =>
        {
            count.IsRequired();
            count.Property(x => x.Total).HasColumnName(nameof(QuestionCount.Total));
            count.Property(x => x.Correct).HasColumnName(nameof(QuestionCount.Correct));
            count.Property(x => x.Wrong).HasColumnName(nameof(QuestionCount.Wrong));
            count.Ignore(x => x.Blank);
        });

        builder.ToTable(table => table.HasCheckConstraint(checkConstraintName, CheckConstraintSql));

        return builder;
    }

    private static void EnsureNoConflict(IReadOnlyTypeBase entityType)
    {
        var entityName = entityType.DisplayName();

        if (entityType.GetComplexProperties().Any(complex => complex.ClrType == typeof(QuestionCount)))
        {
            throw new InvalidOperationException(
                $"The entity '{entityName}' already has a {nameof(QuestionCount)} property; the shared mapping supports one per entity.");
        }

        foreach (var (path, columnName) in ColumnNames(entityType, string.Empty))
        {
            if (ReservedColumns.Contains(columnName, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The entity '{entityName}' already maps '{path}' to the column '{columnName}', which the {nameof(QuestionCount)} mapping reserves.");
            }
        }
    }

    private static IEnumerable<(string Path, string ColumnName)> ColumnNames(IReadOnlyTypeBase type, string prefix)
    {
        foreach (var scalar in type.GetProperties())
        {
            yield return (prefix + scalar.Name, scalar.GetColumnName());
        }

        foreach (var complex in type.GetComplexProperties())
        {
            foreach (var column in ColumnNames(complex.ComplexType, $"{prefix}{complex.Name}."))
            {
                yield return column;
            }
        }
    }
}
