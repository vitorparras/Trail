using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record SearchPlanTopicsQuery(
    StudyPlanId PlanId,
    SubjectId? SubjectId = null,
    TopicStatus? Status = null,
    string? SearchQuery = null);

public sealed class SearchPlanTopicsHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public SearchPlanTopicsHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result<IReadOnlyList<TopicTreeItemView>>> HandleAsync(
        SearchPlanTopicsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_currentUser.IsAuthenticated)
        {
            return Result.Forbidden(Message.Error("auth.unauthenticated"));
        }

        var plan = await _store.GetStudyPlanByIdAsync(query.PlanId, cancellationToken);
        if (plan is null)
        {
            return Result.NotFound(Message.Error("planning.plan.not_found"));
        }

        var subjects = await _store.ListSubjectsByPlanIdAsync(query.PlanId, cancellationToken);
        var subjectsById = subjects.ToDictionary(s => s.Id);

        var orderedSubjects = new List<Subject>();
        foreach (var placement in plan.Outline.OrderBy(p => p.Order))
        {
            if (subjectsById.TryGetValue(placement.SubjectId, out var subject))
            {
                if (!query.SubjectId.HasValue || subject.Id == query.SubjectId.Value)
                {
                    orderedSubjects.Add(subject);
                }
            }
        }

        var allTopicIds = orderedSubjects.SelectMany(s => s.Topics.Select(t => t.Id)).ToList();
        var notes = await _store.ListTopicNotesByTopicIdsAsync(allTopicIds, cancellationToken);
        var noteTopicIds = notes.Select(n => n.TopicId).ToHashSet();

        var normalizedSearch = !string.IsNullOrWhiteSpace(query.SearchQuery)
            ? TextNormalizer.NormalizeSearchText(query.SearchQuery)
            : null;

        var results = new List<TopicTreeItemView>();
        foreach (var subject in orderedSubjects)
        {
            var treeItems = PlanningTopicTreeBuilder.BuildTree(
                subject,
                noteTopicIds,
                query.Status,
                normalizedSearch);

            results.AddRange(treeItems);
        }

        return Result.Ok<IReadOnlyList<TopicTreeItemView>>(results);
    }
}
