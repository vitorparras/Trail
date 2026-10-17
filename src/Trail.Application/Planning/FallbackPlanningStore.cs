using Trail.Domain.Planning;

namespace Trail.Application.Planning;

internal sealed class FallbackPlanningStore : IPlanningStore
{
    public Task<StudyPlan?> GetStudyPlanByIdAsync(StudyPlanId planId, CancellationToken cancellationToken = default) =>
        Task.FromResult<StudyPlan?>(null);

    public Task<IReadOnlyList<StudyPlan>> ListStudyPlansAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StudyPlan>>([]);

    public Task AddStudyPlanAsync(StudyPlan plan, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveStudyPlanAsync(StudyPlan plan, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Subject?> GetSubjectByIdAsync(SubjectId subjectId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Subject?>(null);

    public Task<Subject?> ReloadSubjectByIdAsync(SubjectId subjectId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Subject?>(null);

    public Task<IReadOnlyList<Subject>> ListSubjectsByPlanIdAsync(StudyPlanId planId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Subject>>([]);

    public Task AddSubjectAsync(Subject subject, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveSubjectAsync(Subject subject, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<TopicNote?> GetTopicNoteByTopicIdAsync(TopicId topicId, CancellationToken cancellationToken = default) =>
        Task.FromResult<TopicNote?>(null);

    public Task<IReadOnlyList<TopicNote>> ListTopicNotesByTopicIdsAsync(IReadOnlyCollection<TopicId> topicIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TopicNote>>([]);

    public Task AddTopicNoteAsync(TopicNote note, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveTopicNotesByTopicIdsAsync(IReadOnlyCollection<TopicId> topicIds, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
