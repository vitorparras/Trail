using Trail.Domain.Planning;

namespace Trail.Application.Planning;

internal static class PlanningTopicTreeBuilder
{
    public static List<TopicTreeItemView> BuildTree(
        Subject subject,
        IReadOnlySet<TopicId> noteTopicIds,
        TopicStatus? statusFilter = null,
        string? normalizedSearchQuery = null)
    {
        var result = new List<TopicTreeItemView>();
        AppendChildren(subject, null, 0, noteTopicIds, statusFilter, normalizedSearchQuery, result);
        return result;
    }

    private static void AppendChildren(
        Subject subject,
        TopicId? parentId,
        int depth,
        IReadOnlySet<TopicId> noteTopicIds,
        TopicStatus? statusFilter,
        string? normalizedSearchQuery,
        List<TopicTreeItemView> result)
    {
        var children = subject.Topics
            .Where(t => t.ParentId == parentId)
            .OrderBy(t => t.Order);

        foreach (var topic in children)
        {
            var matchesStatus = !statusFilter.HasValue || topic.Status == statusFilter.Value;
            var matchesSearch = string.IsNullOrEmpty(normalizedSearchQuery)
                || topic.SearchText.Contains(normalizedSearchQuery, StringComparison.Ordinal);

            if (matchesStatus && matchesSearch)
            {
                result.Add(new TopicTreeItemView(
                    topic.Id,
                    subject.Id,
                    subject.Name,
                    topic.ParentId,
                    topic.Code?.Value,
                    topic.Title,
                    topic.Order,
                    topic.Status,
                    noteTopicIds.Contains(topic.Id),
                    depth));
            }

            AppendChildren(subject, topic.Id, depth + 1, noteTopicIds, statusFilter, normalizedSearchQuery, result);
        }
    }
}
