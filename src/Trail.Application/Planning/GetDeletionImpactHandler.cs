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
    private const string SectionsCode = "planning.deletion.sections";
    private const string SubjectsCode = "planning.deletion.subjects";
    private const string TopicsCode = "planning.deletion.topics";
    private const string NotesCode = "planning.deletion.notes";

    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;
    private readonly IReadOnlyList<IDeletionImpactContributor> _contributors;

    public GetDeletionImpactHandler(
        ICurrentUser currentUser,
        IPlanningStore store,
        IEnumerable<IDeletionImpactContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(contributors);

        _currentUser = currentUser;
        _store = store;
        _contributors = [.. contributors];
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

        var resolution = query.TargetKind switch
        {
            DeletionTargetKind.StudyPlan => await ResolvePlanAsync(query, cancellationToken),
            DeletionTargetKind.Section => await ResolveSectionAsync(query, cancellationToken),
            DeletionTargetKind.Subject => await ResolveSubjectAsync(query, cancellationToken),
            DeletionTargetKind.Topic => await ResolveTopicAsync(query, cancellationToken),
            _ => Result.Invalid(Message.Error("planning.deletion.invalid_target_kind")),
        };

        if (!resolution.IsOk)
        {
            return resolution.Failure;
        }

        var target = resolution.Data;
        var contributed = new List<DeletionImpactItem>();
        foreach (var contributor in _contributors)
        {
            contributed.AddRange(await contributor.GetImpactAsync(target.Scope, cancellationToken));
        }

        return Result.Ok(new DeletionImpactView(
            query.TargetKind,
            target.Id,
            target.Name,
            [.. target.Items, .. contributed.OrderBy(item => item.Code, StringComparer.Ordinal)]));
    }

    private async Task<Result<DeletionTarget>> ResolvePlanAsync(
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

        return Result.Ok(new DeletionTarget(
            plan.Id.Value,
            plan.Name,
            new DeletionScope(DeletionTargetKind.StudyPlan, plan.Id, [.. subjects.Select(s => s.Id)], topicIds),
            [
                DeletionImpactItem.Create(SectionsCode, plan.Sections.Count),
                DeletionImpactItem.Create(SubjectsCode, subjects.Count),
                DeletionImpactItem.Create(TopicsCode, topicIds.Count),
                DeletionImpactItem.Create(NotesCode, notes.Count),
            ]));
    }

    private async Task<Result<DeletionTarget>> ResolveSectionAsync(
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

        return Result.Ok(new DeletionTarget(
            section.Id.Value,
            section.Name,
            new DeletionScope(DeletionTargetKind.Section, plan.Id, [], []),
            [DeletionImpactItem.Create(SectionsCode, 1)]));
    }

    private async Task<Result<DeletionTarget>> ResolveSubjectAsync(
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

        return Result.Ok(new DeletionTarget(
            subject.Id.Value,
            subject.Name,
            new DeletionScope(DeletionTargetKind.Subject, subject.StudyPlanId, [subject.Id], topicIds),
            [
                DeletionImpactItem.Create(SubjectsCode, 1),
                DeletionImpactItem.Create(TopicsCode, topicIds.Count),
                DeletionImpactItem.Create(NotesCode, notes.Count),
            ]));
    }

    private async Task<Result<DeletionTarget>> ResolveTopicAsync(
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

        return Result.Ok(new DeletionTarget(
            topic.Id.Value,
            topic.Title,
            new DeletionScope(DeletionTargetKind.Topic, subject.StudyPlanId, [], subtreeIds),
            [
                DeletionImpactItem.Create(TopicsCode, subtreeIds.Count),
                DeletionImpactItem.Create(NotesCode, notes.Count),
            ]));
    }

    private sealed record DeletionTarget(
        Guid Id,
        string Name,
        DeletionScope Scope,
        IReadOnlyList<DeletionImpactItem> Items);
}
