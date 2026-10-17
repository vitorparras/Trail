using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record ListStudyPlansQuery();

public sealed class ListStudyPlansHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public ListStudyPlansHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result<IReadOnlyList<StudyPlanSummaryView>>> HandleAsync(
        ListStudyPlansQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Forbidden(Message.Error("auth.unauthenticated"));
        }

        var plans = await _store.ListStudyPlansAsync(cancellationToken);
        var summaries = new List<StudyPlanSummaryView>(plans.Count);

        foreach (var plan in plans)
        {
            var subjects = await _store.ListSubjectsByPlanIdAsync(plan.Id, cancellationToken);
            var topicCount = subjects.Sum(s => s.Topics.Count);
            var studiedTopicCount = subjects.Sum(s => s.Topics.Count(t => t.Status == TopicStatus.Studied));

            summaries.Add(new StudyPlanSummaryView(
                plan.Id,
                plan.Name,
                plan.Description,
                plan.ScoringRule,
                plan.ExamDate,
                plan.FitnessTestEnabled,
                plan.Sections.Count,
                subjects.Count,
                topicCount,
                studiedTopicCount,
                plan.Version));
        }

        return Result.Ok<IReadOnlyList<StudyPlanSummaryView>>(summaries);
    }
}
