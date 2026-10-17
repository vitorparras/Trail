
using Trail.Domain.Common;

namespace Trail.Domain.Planning;

public sealed class Subject : AggregateRoot<SubjectId>
{
    public const int MaxNameLength = 200;
    public const int MaxTopicTitleLength = 500;

    private readonly List<Topic> _topics = [];

    private Subject()
    {
    }

    private Subject(SubjectId id, UserId ownerId, StudyPlanId studyPlanId, string name)
        : base(id, ownerId)
    {
        StudyPlanId = studyPlanId;
        Name = name;
    }

    public StudyPlanId StudyPlanId { get; private init; }

    public string Name { get; private set; } = string.Empty;

    public IReadOnlyList<Topic> Topics => _topics.OrderBy(t => t.Order).ToList();

    public static Result<Subject> Create(UserId ownerId, StudyPlanId studyPlanId, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Invalid(Message.Error("planning.subject.name_required", nameof(Name)));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > MaxNameLength)
        {
            return Result.Invalid(Message.Error("planning.subject.name_too_long", nameof(Name)));
        }

        return Result.Ok(new Subject(SubjectId.New(), ownerId, studyPlanId, trimmedName));
    }

    public Result Rename(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Invalid(Message.Error("planning.subject.name_required", nameof(Name)));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > MaxNameLength)
        {
            return Result.Invalid(Message.Error("planning.subject.name_too_long", nameof(Name)));
        }

        Name = trimmedName;
        return Result.Ok();
    }

    public Result<Topic> AddTopic(
        string? title,
        TopicCode? code = null,
        TopicId? parentId = null,
        TopicStatus initialStatus = TopicStatus.ToStudy)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Invalid(Message.Error("planning.topic.title_required", nameof(Topic.Title)));
        }

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length > MaxTopicTitleLength)
        {
            return Result.Invalid(Message.Error("planning.topic.title_too_long", nameof(Topic.Title)));
        }

        if (parentId.HasValue && !_topics.Any(t => t.Id == parentId.Value))
        {
            return Result.NotFound(Message.Error("planning.topic.parent_not_found", nameof(Topic.ParentId)));
        }

        if (code is not null && _topics.Any(t => t.Code == code))
        {
            return Result.Conflict(Message.Error("planning.topic.duplicate_code", nameof(Topic.Code)));
        }

        var siblings = _topics.Where(t => t.ParentId == parentId).ToList();
        var order = siblings.Count == 0 ? 0 : siblings.Max(t => t.Order) + 1;
        var searchText = TextNormalizer.NormalizeSearchText(code?.Value, trimmedTitle);
        var topic = new Topic(TopicId.New(), Id, parentId, code, trimmedTitle, order, initialStatus, searchText);
        _topics.Add(topic);

        return Result.Ok(topic);
    }

    public Result UpdateTopic(TopicId topicId, string? title, TopicCode? code)
    {
        var topic = _topics.FirstOrDefault(t => t.Id == topicId);
        if (topic is null)
        {
            return Result.NotFound(Message.Error("planning.topic.not_found", nameof(TopicId)));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Invalid(Message.Error("planning.topic.title_required", nameof(Topic.Title)));
        }

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length > MaxTopicTitleLength)
        {
            return Result.Invalid(Message.Error("planning.topic.title_too_long", nameof(Topic.Title)));
        }

        if (code is not null && _topics.Any(t => t.Id != topicId && t.Code == code))
        {
            return Result.Conflict(Message.Error("planning.topic.duplicate_code", nameof(Topic.Code)));
        }

        var searchText = TextNormalizer.NormalizeSearchText(code?.Value, trimmedTitle);
        topic.Update(trimmedTitle, code, searchText);

        return Result.Ok();
    }

    public Result MoveTopic(TopicId topicId, TopicId? newParentId)
    {
        var topic = _topics.FirstOrDefault(t => t.Id == topicId);
        if (topic is null)
        {
            return Result.NotFound(Message.Error("planning.topic.not_found", nameof(TopicId)));
        }

        if (newParentId.HasValue)
        {
            if (newParentId.Value == topicId)
            {
                return Result.Invalid(Message.Error("planning.topic.cycle_detected", nameof(Topic.ParentId)));
            }

            var parent = _topics.FirstOrDefault(t => t.Id == newParentId.Value);
            if (parent is null)
            {
                return Result.NotFound(Message.Error("planning.topic.parent_not_found", nameof(Topic.ParentId)));
            }

            var descendants = GetSubtreeTopicIds(topicId);
            if (descendants.Contains(newParentId.Value))
            {
                return Result.Invalid(Message.Error("planning.topic.cycle_detected", nameof(Topic.ParentId)));
            }
        }

        if (topic.ParentId == newParentId)
        {
            return Result.Ok();
        }

        var oldParentId = topic.ParentId;
        var newSiblings = _topics.Where(t => t.Id != topicId && t.ParentId == newParentId).ToList();
        var newOrder = newSiblings.Count == 0 ? 0 : newSiblings.Max(t => t.Order) + 1;
        topic.SetParentAndOrder(newParentId, newOrder);

        var remainingOldSiblings = _topics.Where(t => t.ParentId == oldParentId).OrderBy(t => t.Order).ToList();
        for (var i = 0; i < remainingOldSiblings.Count; i++)
        {
            remainingOldSiblings[i].SetOrder(i);
        }

        return Result.Ok();
    }

    public Result ReorderTopics(TopicId? parentId, IReadOnlyList<TopicId> orderedSiblingIds)
    {
        ArgumentNullException.ThrowIfNull(orderedSiblingIds);

        if (parentId.HasValue && !_topics.Any(t => t.Id == parentId.Value))
        {
            return Result.NotFound(Message.Error("planning.topic.parent_not_found", nameof(Topic.ParentId)));
        }

        var siblings = _topics.Where(t => t.ParentId == parentId).ToList();
        if (orderedSiblingIds.Count != siblings.Count
            || orderedSiblingIds.Distinct().Count() != siblings.Count
            || !orderedSiblingIds.All(id => siblings.Any(s => s.Id == id)))
        {
            return Result.Invalid(Message.Error("planning.topic.invalid_reorder", nameof(orderedSiblingIds)));
        }

        for (var i = 0; i < orderedSiblingIds.Count; i++)
        {
            var topic = siblings.First(s => s.Id == orderedSiblingIds[i]);
            topic.SetOrder(i);
        }

        return Result.Ok();
    }

    public Result ChangeTopicStatus(TopicId topicId, TopicStatus newStatus)
    {
        var topic = _topics.FirstOrDefault(t => t.Id == topicId);
        if (topic is null)
        {
            return Result.NotFound(Message.Error("planning.topic.not_found", nameof(TopicId)));
        }

        if (topic.Status == newStatus)
        {
            return Result.Ok();
        }

        var previous = topic.Status;
        topic.SetStatus(newStatus);
        Raise(new TopicStatusChanged(Id, topicId, previous, newStatus));

        return Result.Ok();
    }

    public Result<IReadOnlyList<TopicId>> RemoveTopicSubtree(TopicId topicId)
    {
        var topic = _topics.FirstOrDefault(t => t.Id == topicId);
        if (topic is null)
        {
            return Result.NotFound(Message.Error("planning.topic.not_found", nameof(TopicId)));
        }

        var subtreeIds = GetSubtreeTopicIds(topicId);
        var parentId = topic.ParentId;

        _topics.RemoveAll(t => subtreeIds.Contains(t.Id));

        var siblings = _topics.Where(t => t.ParentId == parentId).OrderBy(t => t.Order).ToList();
        for (var i = 0; i < siblings.Count; i++)
        {
            siblings[i].SetOrder(i);
        }

        return Result.Ok<IReadOnlyList<TopicId>>(subtreeIds);
    }

    public IReadOnlyList<TopicId> GetSubtreeTopicIds(TopicId topicId)
    {
        var result = new List<TopicId> { topicId };
        var queue = new Queue<TopicId>();
        queue.Enqueue(topicId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var children = _topics.Where(t => t.ParentId == current).Select(t => t.Id);
            foreach (var child in children)
            {
                result.Add(child);
                queue.Enqueue(child);
            }
        }

        return result;
    }
}
