using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public interface IPlanningStore
{
    Task<StudyPlan?> GetStudyPlanByIdAsync(StudyPlanId planId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudyPlan>> ListStudyPlansAsync(CancellationToken cancellationToken = default);

    Task AddStudyPlanAsync(StudyPlan plan, CancellationToken cancellationToken = default);

    Task RemoveStudyPlanAsync(StudyPlan plan, CancellationToken cancellationToken = default);

    Task<Subject?> GetSubjectByIdAsync(SubjectId subjectId, CancellationToken cancellationToken = default);

    Task<Subject?> ReloadSubjectByIdAsync(SubjectId subjectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Subject>> ListSubjectsByPlanIdAsync(StudyPlanId planId, CancellationToken cancellationToken = default);

    Task AddSubjectAsync(Subject subject, CancellationToken cancellationToken = default);

    Task RemoveSubjectAsync(Subject subject, CancellationToken cancellationToken = default);

    Task<TopicNote?> GetTopicNoteByTopicIdAsync(TopicId topicId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopicNote>> ListTopicNotesByTopicIdsAsync(IReadOnlyCollection<TopicId> topicIds, CancellationToken cancellationToken = default);

    Task AddTopicNoteAsync(TopicNote note, CancellationToken cancellationToken = default);

    Task RemoveTopicNotesByTopicIdsAsync(IReadOnlyCollection<TopicId> topicIds, CancellationToken cancellationToken = default);

    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default);
}
