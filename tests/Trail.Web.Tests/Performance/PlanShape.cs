namespace Trail.Web.Tests.Performance;

public sealed record PlanShape(
    int Sections,
    int Subjects,
    int Topics,
    int MaxTopicDepth,
    int NoteEvery)
{
    public static PlanShape Default { get; } = new(
        Sections: 4,
        Subjects: 12,
        Topics: 1000,
        MaxTopicDepth: 3,
        NoteEvery: 10);

    public IReadOnlyList<int> TopicsPerSubject()
    {
        var weightSum = Subjects * (Subjects + 1) / 2;
        var counts = new int[Subjects];
        var assigned = 0;
        for (var index = 0; index < Subjects; index++)
        {
            counts[index] = Topics * (Subjects - index) / weightSum;
            assigned += counts[index];
        }

        for (var index = 0; assigned < Topics; index = (index + 1) % Subjects, assigned++)
        {
            counts[index]++;
        }

        return counts;
    }
}
