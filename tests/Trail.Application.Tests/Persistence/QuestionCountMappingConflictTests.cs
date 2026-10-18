using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Trail.Domain.Practice;
using Trail.Infrastructure.Persistence.Configurations.Practice;

namespace Trail.Application.Tests.Persistence;

public sealed class QuestionCountMappingConflictTests
{
    [Fact]
    public void HasQuestionCount_WhenTheEntityHasItsOwnTotalProperty_ThrowsAClearError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BuildModel(builder => builder.Entity<EntityWithOwnTotal>(b =>
            {
                b.HasKey(x => x.Id);
                b.HasQuestionCount(x => x.Count, "CK_EntityWithOwnTotal_Count");
            })));

        Assert.Contains(nameof(EntityWithOwnTotal.Total), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(EntityWithOwnTotal), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HasQuestionCount_WhenAPropertyIsMappedToAReservedColumn_ThrowsAClearError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BuildModel(builder => builder.Entity<EntityWithRenamedColumn>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Score).HasColumnName("Correct");
                b.HasQuestionCount(x => x.Count, "CK_EntityWithRenamedColumn_Count");
            })));

        Assert.Contains(nameof(EntityWithRenamedColumn.Score), exception.Message, StringComparison.Ordinal);
        Assert.Contains("Correct", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HasQuestionCount_WhenAnotherComplexPropertyMapsAReservedColumn_ThrowsAClearError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BuildModel(builder => builder.Entity<EntityWithComplexWrong>(b =>
            {
                b.HasKey(x => x.Id);
                b.ComplexProperty(x => x.Extra, extra => extra.Property(x => x.Wrong).HasColumnName("Wrong"));
                b.HasQuestionCount(x => x.Count, "CK_EntityWithComplexWrong_Count");
            })));

        Assert.Contains("Extra.Wrong", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HasQuestionCount_CalledTwiceForTheSameEntity_ThrowsAClearError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BuildModel(builder => builder.Entity<EntityWithTwoCounts>(b =>
            {
                b.HasKey(x => x.Id);
                b.HasQuestionCount(x => x.First, "CK_EntityWithTwoCounts_First");
                b.HasQuestionCount(x => x.Second, "CK_EntityWithTwoCounts_Second");
            })));

        Assert.Contains(nameof(QuestionCount), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(EntityWithTwoCounts), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HasQuestionCount_WithoutConflicts_BuildsTheModel()
    {
        var model = BuildModel(builder => builder.Entity<EntityWithoutConflict>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasQuestionCount(x => x.Count, "CK_EntityWithoutConflict_Count");
        }));

        Assert.NotNull(model);
    }

    private static IModel BuildModel(Action<ModelBuilder> configure)
    {
        using var context = new ConflictDbContext(configure);

        return context.Model;
    }

    private sealed class ConflictDbContext(Action<ModelBuilder> configure) : DbContext(
        new DbContextOptionsBuilder<ConflictDbContext>()
            .UseSqlite("Data Source=:memory:")
            .ReplaceService<IModelCacheKeyFactory, ConfigurationCacheKeyFactory>()
            .Options)
    {
        public Action<ModelBuilder> Configure { get; } = configure;

        protected override void OnModelCreating(ModelBuilder modelBuilder) => Configure(modelBuilder);
    }

    private sealed class ConfigurationCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) =>
            ((ConflictDbContext)context).Configure;
    }

    private sealed class EntityWithOwnTotal
    {
        public Guid Id { get; set; }

        public int Total { get; set; }

        public QuestionCount Count { get; set; } = null!;
    }

    private sealed class EntityWithRenamedColumn
    {
        public Guid Id { get; set; }

        public int Score { get; set; }

        public QuestionCount Count { get; set; } = null!;
    }

    private sealed class EntityWithComplexWrong
    {
        public Guid Id { get; set; }

        public ExtraValues Extra { get; set; } = null!;

        public QuestionCount Count { get; set; } = null!;
    }

    private sealed class ExtraValues
    {
        public int Wrong { get; set; }
    }

    private sealed class EntityWithTwoCounts
    {
        public Guid Id { get; set; }

        public QuestionCount First { get; set; } = null!;

        public QuestionCount Second { get; set; } = null!;
    }

    private sealed class EntityWithoutConflict
    {
        public Guid Id { get; set; }

        public QuestionCount Count { get; set; } = null!;
    }
}
