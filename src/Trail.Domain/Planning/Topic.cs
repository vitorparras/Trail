namespace Trail.Domain.Planning;

public sealed class Topic
{
    private Topic()
    {
    }

    internal Topic(
        TopicId id,
        SubjectId subjectId,
        TopicId? parentId,
        TopicCode? code,
        string title,
        int order,
        TopicStatus status,
        string searchText)
    {
        Id = id;
        SubjectId = subjectId;
        ParentId = parentId;
        Code = code;
        Title = title;
        Order = order;
        Status = status;
        SearchText = searchText;
    }

    public TopicId Id { get; private init; }

    public SubjectId SubjectId { get; private init; }

    public TopicId? ParentId { get; private set; }

    public TopicCode? Code { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public int Order { get; private set; }

    public TopicStatus Status { get; private set; }

    public string SearchText { get; private set; } = string.Empty;

    internal void Update(string title, TopicCode? code, string searchText)
    {
        Title = title;
        Code = code;
        SearchText = searchText;
    }

    internal void SetParentAndOrder(TopicId? parentId, int order)
    {
        ParentId = parentId;
        Order = order;
    }

    internal void SetOrder(int order) => Order = order;

    internal void SetStatus(TopicStatus status) => Status = status;
}
