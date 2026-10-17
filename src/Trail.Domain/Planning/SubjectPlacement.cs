namespace Trail.Domain.Planning;

public sealed class SubjectPlacement
{
    private SubjectPlacement()
    {
    }

    internal SubjectPlacement(StudyPlanId studyPlanId, SubjectId subjectId, SectionId? sectionId, int order)
    {
        StudyPlanId = studyPlanId;
        SubjectId = subjectId;
        SectionId = sectionId;
        Order = order;
    }

    public StudyPlanId StudyPlanId { get; private init; }

    public SubjectId SubjectId { get; private init; }

    public SectionId? SectionId { get; private set; }

    public int Order { get; private set; }

    internal void SetSection(SectionId? sectionId) => SectionId = sectionId;

    internal void SetOrder(int order) => Order = order;
}
