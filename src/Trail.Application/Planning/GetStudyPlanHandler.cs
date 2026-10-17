using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record GetStudyPlanQuery(StudyPlanId PlanId);

public sealed class GetStudyPlanHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public GetStudyPlanHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result<StudyPlanDetailView>> HandleAsync(
        GetStudyPlanQuery query,
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

        var sections = plan.Sections
            .OrderBy(s => s.Order)
            .Select(s => new SectionView(s.Id, s.Name, s.Order))
            .ToList();

        var outlineSubjects = new List<SubjectOutlineItemView>();
        foreach (var placement in plan.Outline.OrderBy(p => p.Order))
        {
            if (subjectsById.TryGetValue(placement.SubjectId, out var subject))
            {
                var topicCount = subject.Topics.Count;
                var toStudy = subject.Topics.Count(t => t.Status == TopicStatus.ToStudy);
                var studying = subject.Topics.Count(t => t.Status == TopicStatus.Studying);
                var studied = subject.Topics.Count(t => t.Status == TopicStatus.Studied);
                var reviewing = subject.Topics.Count(t => t.Status == TopicStatus.Reviewing);

                outlineSubjects.Add(new SubjectOutlineItemView(
                    subject.Id,
                    subject.Name,
                    placement.SectionId,
                    placement.Order,
                    topicCount,
                    toStudy,
                    studying,
                    studied,
                    reviewing,
                    subject.Version));
            }
        }

        var totalTopicCount = subjects.Sum(s => s.Topics.Count);
        var totalToStudy = subjects.Sum(s => s.Topics.Count(t => t.Status == TopicStatus.ToStudy));
        var totalStudying = subjects.Sum(s => s.Topics.Count(t => t.Status == TopicStatus.Studying));
        var totalStudied = subjects.Sum(s => s.Topics.Count(t => t.Status == TopicStatus.Studied));
        var totalReviewing = subjects.Sum(s => s.Topics.Count(t => t.Status == TopicStatus.Reviewing));

        return Result.Ok(new StudyPlanDetailView(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.ScoringRule,
            plan.DefaultPerformancePeriod,
            plan.PerformanceBands,
            plan.ExamDate,
            plan.DefaultMockExamQuestions,
            plan.FitnessTestEnabled,
            sections,
            outlineSubjects,
            totalTopicCount,
            totalToStudy,
            totalStudying,
            totalStudied,
            totalReviewing,
            plan.Version));
    }
}
