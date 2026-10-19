using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Trail.Domain.Common;
using Trail.Domain.Practice;
using Trail.Infrastructure.Persistence;

namespace Trail.Application.Tests.Persistence;

public sealed class QuestionCountMappingTests
{
    private const string ExpectedCheckSql =
        "\"Total\" > 0 AND \"Correct\" >= 0 AND \"Wrong\" >= 0 AND \"Correct\" + \"Wrong\" <= \"Total\"";

    private const string TableName = "QuestionCountSamples";

    public static TheoryData<DatabaseProvider, int, int, int> InvalidCounts
    {
        get
        {
            var data = new TheoryData<DatabaseProvider, int, int, int>();
            foreach (var provider in ProvidersToRun())
            {
                data.Add(provider, 0, 0, 0);
                data.Add(provider, -1, 0, 0);
                data.Add(provider, 5, -1, 0);
                data.Add(provider, 5, 0, -1);
                data.Add(provider, 5, 3, 3);
                data.Add(provider, 5, 6, 0);
            }

            return data;
        }
    }

    public static TheoryData<DatabaseProvider, int, int, int> ValidCounts
    {
        get
        {
            var data = new TheoryData<DatabaseProvider, int, int, int>();
            foreach (var provider in ProvidersToRun())
            {
                data.Add(provider, 1, 0, 0);
                data.Add(provider, 5, 5, 0);
                data.Add(provider, 5, 0, 5);
                data.Add(provider, 5, 3, 2);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task Model_DeclaresTheSharedCheckConstraint(DatabaseProvider provider)
    {
        await using var harness = await TestDbContextFixture.CreateAsync(provider, null, TestContext.Current.CancellationToken);
        await using var context = harness.CreateContext(new TestCurrentUser());

        var entityType = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(QuestionCountSample))!;
        var constraint = Assert.Single(entityType.GetCheckConstraints());

        Assert.Equal(QuestionCountSample.CheckConstraintName, constraint.Name);
        Assert.Equal(ExpectedCheckSql, constraint.Sql);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task Table_HasTheUnprefixedQuestionCountColumns(DatabaseProvider provider)
    {
        await using var harness = await TestDbContextFixture.CreateAsync(provider, null, TestContext.Current.CancellationToken);
        await using var context = harness.CreateContext(new TestCurrentUser());

        var columns = provider == DatabaseProvider.Sqlite
            ? await context.Database
                .SqlQueryRaw<string>($"SELECT name AS \"Value\" FROM pragma_table_info('{TableName}')")
                .ToListAsync(TestContext.Current.CancellationToken)
            : await context.Database
                .SqlQueryRaw<string>($"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = '{TableName}'")
                .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Total", columns);
        Assert.Contains("Correct", columns);
        Assert.Contains("Wrong", columns);
        Assert.DoesNotContain(columns, column => column.Contains("Count_", StringComparison.Ordinal));
        Assert.DoesNotContain("Blank", columns);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task QuestionCount_IsStoredAndRetrievedWithDerivedBlank(DatabaseProvider provider)
    {
        await using var harness = await TestDbContextFixture.CreateAsync(provider, null, TestContext.Current.CancellationToken);
        var user = new TestCurrentUser { IsAuthenticated = true, Id = UserId.New() };
        var id = TestAggregateId.New();

        await using (var context = harness.CreateContext(user))
        {
            context.QuestionCountSamples.Add(new QuestionCountSample(id, user.Id, 1, QuestionCount.Create(10, 6, 3).Data));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = harness.CreateContext(user))
        {
            var loaded = await context.QuestionCountSamples.SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken);

            Assert.Equal(QuestionCount.Create(10, 6, 3).Data, loaded.Count);
            Assert.Equal(1, loaded.Count.Blank);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidCounts))]
    public async Task Check_RejectsAnInvalidRowWrittenByDirectSql(DatabaseProvider provider, int total, int correct, int wrong)
    {
        await using var harness = await TestDbContextFixture.CreateAsync(provider, null, TestContext.Current.CancellationToken);
        var user = new TestCurrentUser { IsAuthenticated = true, Id = UserId.New() };
        var id = await SeedValidRowAsync(harness, user);

        await using var context = harness.CreateContext(user);

        var exception = await Assert.ThrowsAnyAsync<DbException>(() => context.Database.ExecuteSqlAsync(
            $"UPDATE \"QuestionCountSamples\" SET \"Total\" = {total}, \"Correct\" = {correct}, \"Wrong\" = {wrong} WHERE \"Id\" = {id.Value}",
            TestContext.Current.CancellationToken));

        Assert.Contains(QuestionCountSample.CheckConstraintName, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ValidCounts))]
    public async Task Check_AcceptsAValidRowWrittenByDirectSql(DatabaseProvider provider, int total, int correct, int wrong)
    {
        await using var harness = await TestDbContextFixture.CreateAsync(provider, null, TestContext.Current.CancellationToken);
        var user = new TestCurrentUser { IsAuthenticated = true, Id = UserId.New() };
        var id = await SeedValidRowAsync(harness, user);

        await using var context = harness.CreateContext(user);

        var affected = await context.Database.ExecuteSqlAsync(
            $"UPDATE \"QuestionCountSamples\" SET \"Total\" = {total}, \"Correct\" = {correct}, \"Wrong\" = {wrong} WHERE \"Id\" = {id.Value}",
            TestContext.Current.CancellationToken);

        Assert.Equal(1, affected);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task GroupBy_WithSumOverTheComplexType_ReturnsTheSameTotalsOnEveryProvider(DatabaseProvider provider)
    {
        await using var harness = await TestDbContextFixture.CreateAsync(provider, null, TestContext.Current.CancellationToken);
        var user = new TestCurrentUser { IsAuthenticated = true, Id = UserId.New() };

        await using (var context = harness.CreateContext(user))
        {
            context.QuestionCountSamples.AddRange(
                Sample(user, 1, 10, 6, 3),
                Sample(user, 1, 20, 12, 5),
                Sample(user, 1, 5, 0, 0),
                Sample(user, 2, 8, 8, 0),
                Sample(user, 2, 1, 0, 1),
                Sample(user, 3, 100, 40, 40));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = harness.CreateContext(user))
        {
            var sums = await context.QuestionCountSamples
                .GroupBy(x => x.Category)
                .Select(group => new
                {
                    Category = group.Key,
                    Total = group.Sum(x => x.Count.Total),
                    Correct = group.Sum(x => x.Count.Correct),
                    Wrong = group.Sum(x => x.Count.Wrong),
                    Blank = group.Sum(x => x.Count.Total - x.Count.Correct - x.Count.Wrong),
                })
                .OrderBy(x => x.Category)
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Collection(
                sums,
                first =>
                {
                    Assert.Equal((1, 35, 18, 8, 9), (first.Category, first.Total, first.Correct, first.Wrong, first.Blank));
                },
                second =>
                {
                    Assert.Equal((2, 9, 8, 1, 0), (second.Category, second.Total, second.Correct, second.Wrong, second.Blank));
                },
                third =>
                {
                    Assert.Equal((3, 100, 40, 40, 20), (third.Category, third.Total, third.Correct, third.Wrong, third.Blank));
                });
        }
    }

    private static IEnumerable<DatabaseProvider> ProvidersToRun() =>
        TestDatabaseProviders.EnabledProviders.Select(row => row.Data);

    private static QuestionCountSample Sample(TestCurrentUser user, int category, int total, int correct, int wrong) =>
        new(TestAggregateId.New(), user.Id, category, QuestionCount.Create(total, correct, wrong).Data);

    private static async Task<TestAggregateId> SeedValidRowAsync(TestDatabaseHarness harness, TestCurrentUser user)
    {
        var id = TestAggregateId.New();

        await using var context = harness.CreateContext(user);
        context.QuestionCountSamples.Add(new QuestionCountSample(id, user.Id, 1, QuestionCount.Create(10, 4, 4).Data));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }
}
