using Trail.Application.Abstractions;
using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Tests.Planning;

internal sealed class TestCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; } = true;

    public UserId Id { get; set; } = UserId.New();

    public bool IsAdmin { get; set; }
}

internal sealed class FakePlanningStore : IPlanningStore
{
    public Dictionary<StudyPlanId, StudyPlan> Plans { get; } = new();

    public Dictionary<SubjectId, Subject> Subjects { get; } = new();

    public Dictionary<TopicNoteId, TopicNote> TopicNotes { get; } = new();

    public bool SaveChangesResult { get; set; } = true;

    public Queue<bool> SaveChangesQueue { get; } = new();

    public int SaveChangesCalls { get; private set; }

    public int ReloadCalls { get; private set; }

    public Func<SubjectId, Subject?>? ReloadOverride { get; set; }

    public Task<StudyPlan?> GetStudyPlanByIdAsync(StudyPlanId planId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Plans.GetValueOrDefault(planId));

    public Task<IReadOnlyList<StudyPlan>> ListStudyPlansAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StudyPlan>>(Plans.Values.ToList());

    public Task<IReadOnlyList<StudyPlanOptionView>> ListStudyPlanOptionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StudyPlanOptionView>>([.. Plans.Values
            .OrderBy(plan => plan.Name, StringComparer.Ordinal)
            .ThenBy(plan => plan.Id.Value)
            .Select(plan => new StudyPlanOptionView(plan.Id, plan.Name))]);

    public Task AddStudyPlanAsync(StudyPlan plan, CancellationToken cancellationToken = default)
    {
        Plans[plan.Id] = plan;
        return Task.CompletedTask;
    }

    public Task RemoveStudyPlanAsync(StudyPlan plan, CancellationToken cancellationToken = default)
    {
        Plans.Remove(plan.Id);
        return Task.CompletedTask;
    }

    public Task<Subject?> GetSubjectByIdAsync(SubjectId subjectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Subjects.GetValueOrDefault(subjectId));

    public Task<Subject?> ReloadSubjectByIdAsync(SubjectId subjectId, CancellationToken cancellationToken = default)
    {
        ReloadCalls++;
        if (ReloadOverride != null)
        {
            return Task.FromResult(ReloadOverride(subjectId));
        }

        return Task.FromResult(Subjects.GetValueOrDefault(subjectId));
    }

    public Task<IReadOnlyList<Subject>> ListSubjectsByPlanIdAsync(StudyPlanId planId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Subject>>(Subjects.Values.Where(s => s.StudyPlanId == planId).ToList());

    public Task AddSubjectAsync(Subject subject, CancellationToken cancellationToken = default)
    {
        Subjects[subject.Id] = subject;
        return Task.CompletedTask;
    }

    public Task RemoveSubjectAsync(Subject subject, CancellationToken cancellationToken = default)
    {
        Subjects.Remove(subject.Id);
        return Task.CompletedTask;
    }

    public Task<TopicNote?> GetTopicNoteByTopicIdAsync(TopicId topicId, CancellationToken cancellationToken = default) =>
        Task.FromResult(TopicNotes.Values.FirstOrDefault(n => n.TopicId == topicId));

    public Task<IReadOnlyList<TopicNote>> ListTopicNotesByTopicIdsAsync(
        IReadOnlyCollection<TopicId> topicIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TopicNote>>(TopicNotes.Values.Where(n => topicIds.Contains(n.TopicId)).ToList());

    public Task AddTopicNoteAsync(TopicNote note, CancellationToken cancellationToken = default)
    {
        TopicNotes[note.Id] = note;
        return Task.CompletedTask;
    }

    public Task RemoveTopicNotesByTopicIdsAsync(
        IReadOnlyCollection<TopicId> topicIds,
        CancellationToken cancellationToken = default)
    {
        var toRemove = TopicNotes.Values.Where(n => topicIds.Contains(n.TopicId)).Select(n => n.Id).ToList();
        foreach (var id in toRemove)
        {
            TopicNotes.Remove(id);
        }

        return Task.CompletedTask;
    }

    public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        if (SaveChangesQueue.Count > 0)
        {
            return Task.FromResult(SaveChangesQueue.Dequeue());
        }

        return Task.FromResult(SaveChangesResult);
    }
}
