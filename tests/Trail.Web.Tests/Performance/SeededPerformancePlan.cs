using Trail.Domain.Planning;

namespace Trail.Web.Tests.Performance;

public sealed record SeededPerformanceSubject(SubjectId Id, string Name, IReadOnlyList<TopicId> TopicIds);

public sealed record SeededPerformancePlan(
    StudyPlanId PlanId,
    string Name,
    IReadOnlyList<SectionId> SectionIds,
    IReadOnlyList<SeededPerformanceSubject> Subjects,
    int NoteCount)
{
    public int TopicCount => Subjects.Sum(subject => subject.TopicIds.Count);

    public IReadOnlyList<TopicId> TopicIds => [.. Subjects.SelectMany(subject => subject.TopicIds)];

    public SeededPerformanceSubject LargestSubject => Subjects.MaxBy(subject => subject.TopicIds.Count)!;

    public string PlanPath => $"/planos/{PlanId.Value}";

    public string SubjectPath(SeededPerformanceSubject subject) =>
        $"/planos/{PlanId.Value}/disciplinas/{subject.Id.Value}";
}
