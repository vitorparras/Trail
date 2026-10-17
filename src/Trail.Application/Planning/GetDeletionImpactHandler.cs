using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record GetDeletionImpactQuery(
    DeletionTargetKind TargetKind,
    StudyPlanId PlanId,
    SectionId? SectionId = null,
    SubjectId? SubjectId = null,
    TopicId? TopicId = null);

public sealed class GetDeletionImpactHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public GetDeletionImpactHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result<DeletionImpactView>> HandleAsync(
        GetDeletionImpactQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_currentUser.IsAuthenticated)
        {
            return Result.Forbidden(Message.Error("auth.unauthenticated"));
        }

        return query.TargetKind switch
        {
            DeletionTargetKind.StudyPlan => await GetPlanImpactAsync(query, cancellationToken),
            DeletionTargetKind.Section => await GetSectionImpactAsync(query, cancellationToken),
            DeletionTargetKind.Subject => await GetSubjectImpactAsync(query, cancellationToken),
            DeletionTargetKind.Topic => await GetTopicImpactAsync(query, cancellationToken),
            _ => Result.Invalid(Message.Error("planning.deletion.invalid_target_kind")),
        };
    }

    private async Task<Result<DeletionImpactView>> GetPlanImpactAsync(
        GetDeletionImpactQuery query,
        CancellationToken cancellationToken)
    {
        var plan = await _store.GetStudyPlanByIdAsync(query.PlanId, cancellationToken);
        if (plan is null)
        {
            return Result.NotFound(Message.Error("planning.plan.not_found"));
        }

        var subjects = await _store.ListSubjectsByPlanIdAsync(plan.Id, cancellationToken);
        var topicIds = subjects.SelectMany(s => s.Topics.Select(t => t.Id)).ToList();
        var notes = await _store.ListTopicNotesByTopicIdsAsync(topicIds, cancellationToken);

        return Result.Ok(new DeletionImpactView(
            DeletionTargetKind.StudyPlan,
            plan.Id.Value,
            plan.Name,
            plan.Sections.Count,
            subjects.Count,
            topicIds.Count,
            notes.Count));
    }

    private async Task<Result<DeletionImpactView>> GetSectionImpactAsync(
        GetDeletionImpactQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.SectionId.HasValue)
        {
            return Result.Invalid(Message.Error("planning.deletion.section_required"));
        }

        var plan = await _store.GetStudyPlanByIdAsync(query.PlanId, cancellationToken);
        if (plan is null)
        {
            return Result.NotFound(Message.Error("planning.plan.not_found"));
        }

        var section = plan.Sections.FirstOrDefault(s => s.Id == query.SectionId.Value);
        if (section is null)
        {
            return Result.NotFound(Message.Error("planning.section.not_found"));
        }

        return Result.Ok(new DeletionImpactView(
            DeletionTargetKind.Section,
            section.Id.Value,
            section.Name,
            1,
            0,
            0,
            0));
    }

    private async Task<Result<DeletionImpactView>> GetSubjectImpactAsync(
        GetDeletionImpactQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.SubjectId.HasValue)
        {
            return Result.Invalid(Message.Error("planning.deletion.subject_required"));
        }

        var subject = await _store.GetSubjectByIdAsync(query.SubjectId.Value, cancellationToken);
        if (subject is null || subject.StudyPlanId != query.PlanId)
        {
            return Result.NotFound(Message.Error("planning.subject.not_found"));
        }

        var topicIds = subject.Topics.Select(t => t.Id).ToList();
        var notes = await _store.ListTopicNotesByTopicIdsAsync(topicIds, cancellationToken);

        return Result.Ok(new DeletionImpactView(
            DeletionTargetKind.Subject,
            subject.Id.Value,
            subject.Name,
            0,
            1,
            topicIds.Count,
            notes.Count));
    }

    private async Task<Result<DeletionImpactView>> GetTopicImpactAsync(
        GetDeletionImpactQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.SubjectId.HasValue || !query.TopicId.HasValue)
        {
            return Result.Invalid(Message.Error("planning.deletion.topic_required"));
        }

        var subject = await _store.GetSubjectByIdAsync(query.SubjectId.Value, cancellationToken);
        if (subject is null || subject.StudyPlanId != query.PlanId)
        {
            return Result.NotFound(Message.Error("planning.subject.not_found"));
        }

        var topic = subject.Topics.FirstOrDefault(t => t.Id == query.TopicId.Value);
        if (topic is null)
        {
            return Result.NotFound(Message.Error("planning.topic.not_found"));
        }

        var subtreeIds = subject.GetSubtreeTopicIds(topic.Id);
        var notes = await _store.ListTopicNotesByTopicIdsAsync(subtreeIds, cancellationToken);

        return Result.Ok(new DeletionImpactView(
            DeletionTargetKind.Topic,
            topic.Id.Value,
            topic.Title,
            0,
            0,
            subtreeIds.Count,
            notes.Count));
    }
}
