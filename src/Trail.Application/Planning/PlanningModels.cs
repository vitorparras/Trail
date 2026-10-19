using Trail.Application.Abstractions;
using Trail.Domain.Planning;

namespace Trail.Application.Planning;

public sealed record StudyPlanSummaryView(
    StudyPlanId Id,
    string Name,
    string Description,
    ScoringRule ScoringRule,
    DateOnly? ExamDate,
    bool FitnessTestEnabled,
    int SectionCount,
    int SubjectCount,
    int TopicCount,
    int StudiedTopicCount,
    int Version);

public sealed record SectionView(
    SectionId Id,
    string Name,
    int Order);

public sealed record SubjectOutlineItemView(
    SubjectId SubjectId,
    string Name,
    SectionId? SectionId,
    int Order,
    int TopicCount,
    int ToStudyCount,
    int StudyingCount,
    int StudiedCount,
    int ReviewingCount,
    int Version);

public sealed record StudyPlanDetailView(
    StudyPlanId Id,
    string Name,
    string Description,
    ScoringRule ScoringRule,
    PerformancePeriod DefaultPerformancePeriod,
    PerformanceBands PerformanceBands,
    DateOnly? ExamDate,
    int DefaultMockExamQuestions,
    bool FitnessTestEnabled,
    IReadOnlyList<SectionView> Sections,
    IReadOnlyList<SubjectOutlineItemView> Subjects,
    int TotalTopicCount,
    int ToStudyCount,
    int StudyingCount,
    int StudiedCount,
    int ReviewingCount,
    int Version);

public sealed record TopicTreeItemView(
    TopicId Id,
    SubjectId SubjectId,
    string SubjectName,
    TopicId? ParentId,
    string? Code,
    string Title,
    int Order,
    TopicStatus Status,
    bool HasNote,
    int Depth);

public sealed record SubjectDetailView(
    SubjectId Id,
    StudyPlanId StudyPlanId,
    string StudyPlanName,
    SectionId? SectionId,
    string? SectionName,
    string Name,
    IReadOnlyList<TopicTreeItemView> Topics,
    int ToStudyCount,
    int StudyingCount,
    int StudiedCount,
    int ReviewingCount,
    int Version);

public sealed record TopicNoteView(
    TopicId TopicId,
    SubjectId SubjectId,
    StudyPlanId StudyPlanId,
    string TopicTitle,
    string? TopicCode,
    TopicNoteId? NoteId,
    string Markdown,
    int Version);

public sealed record DeletionImpactView(
    DeletionTargetKind TargetKind,
    Guid TargetId,
    string TargetName,
    IReadOnlyList<DeletionImpactItem> Items);
