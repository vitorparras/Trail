using Trail.Domain.Common;
using Trail.Domain.Practice;

namespace Trail.Application.Tests.Persistence;

public sealed class QuestionCountSample : AggregateRoot<TestAggregateId>
{
    public const string CheckConstraintName = "CK_QuestionCountSamples_Count";

    private QuestionCountSample()
    {
        Count = null!;
    }

    public QuestionCountSample(TestAggregateId id, UserId ownerId, int category, QuestionCount count)
        : base(id, ownerId)
    {
        ArgumentNullException.ThrowIfNull(count);

        Category = category;
        Count = count;
    }

    public int Category { get; private set; }

    public QuestionCount Count { get; private set; }
}
