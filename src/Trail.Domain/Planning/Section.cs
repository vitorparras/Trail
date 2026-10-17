namespace Trail.Domain.Planning;

public sealed class Section
{
    private Section()
    {
    }

    internal Section(SectionId id, StudyPlanId studyPlanId, string name, int order)
    {
        Id = id;
        StudyPlanId = studyPlanId;
        Name = name;
        Order = order;
    }

    public SectionId Id { get; private init; }

    public StudyPlanId StudyPlanId { get; private init; }

    public string Name { get; private set; } = string.Empty;

    public int Order { get; private set; }

    internal void Rename(string name) => Name = name;

    internal void SetOrder(int order) => Order = order;
}
