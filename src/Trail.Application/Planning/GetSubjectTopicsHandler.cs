using Trail.Application.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record GetSubjectTopicsQuery(SubjectId SubjectId);

public sealed class GetSubjectTopicsHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlanningStore _store;

    public GetSubjectTopicsHandler(ICurrentUser currentUser, IPlanningStore store)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);

        _currentUser = currentUser;
        _store = store;
    }

    public async Task<Result<SubjectDetailView>> HandleAsync(
        GetSubjectTopicsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_currentUser.IsAuthenticated)
        {
            return Result.Forbidden(Message.Error("auth.unauthenticated"));
        }

        var subject = await _store.GetSubjectByIdAsync(query.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Result.NotFound(Message.Error("planning.subject.not_found"));
        }

        var plan = await _store.GetStudyPlanByIdAsync(subject.StudyPlanId, cancellationToken);
        if (plan is null)
        {
            return Result.NotFound(Message.Error("planning.plan.not_found"));
        }

        var placement = plan.Outline.FirstOrDefault(p => p.SubjectId == subject.Id);
        var section = placement?.SectionId.HasValue == true
            ? plan.Sections.FirstOrDefault(s => s.Id == placement.SectionId.Value)
            : null;

        var topicIds = subject.Topics.Select(t => t.Id).ToList();
        var notes = await _store.ListTopicNotesByTopicIdsAsync(topicIds, cancellationToken);
        var noteTopicIds = notes.Select(n => n.TopicId).ToHashSet();

        var topicTree = PlanningTopicTreeBuilder.BuildTree(subject, noteTopicIds);

        var toStudy = subject.Topics.Count(t => t.Status == TopicStatus.ToStudy);
        var studying = subject.Topics.Count(t => t.Status == TopicStatus.Studying);
        var studied = subject.Topics.Count(t => t.Status == TopicStatus.Studied);
        var reviewing = subject.Topics.Count(t => t.Status == TopicStatus.Reviewing);

        return Result.Ok(new SubjectDetailView(
            subject.Id,
            plan.Id,
            plan.Name,
            placement?.SectionId,
            section?.Name,
            subject.Name,
            topicTree,
            toStudy,
            studying,
            studied,
            reviewing,
            subject.Version));
    }
}
