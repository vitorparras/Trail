
using Trail.Domain.Common;

namespace Trail.Domain.Planning;

public readonly record struct StudyPlanId(Guid Value) : ITypedId<StudyPlanId>
{
    public Guid Value { get; } = Value;

    public static StudyPlanId New() => new(Guid.CreateVersion7());

    public static StudyPlanId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public readonly record struct SectionId(Guid Value) : ITypedId<SectionId>
{
    public Guid Value { get; } = Value;

    public static SectionId New() => new(Guid.CreateVersion7());

    public static SectionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public readonly record struct SubjectId(Guid Value) : ITypedId<SubjectId>
{
    public Guid Value { get; } = Value;

    public static SubjectId New() => new(Guid.CreateVersion7());

    public static SubjectId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public readonly record struct TopicId(Guid Value) : ITypedId<TopicId>
{
    public Guid Value { get; } = Value;

    public static TopicId New() => new(Guid.CreateVersion7());

    public static TopicId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public readonly record struct TopicNoteId(Guid Value) : ITypedId<TopicNoteId>
{
    public Guid Value { get; } = Value;

    public static TopicNoteId New() => new(Guid.CreateVersion7());

    public static TopicNoteId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public readonly record struct FitnessTestEventId(Guid Value) : ITypedId<FitnessTestEventId>
{
    public Guid Value { get; } = Value;

    public static FitnessTestEventId New() => new(Guid.CreateVersion7());

    public static FitnessTestEventId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
