using System.Collections.Concurrent;
using System.Reflection;
using Trail.Application.Planning;
using Trail.Domain.Planning;

namespace Trail.Web.Tests.Planning;

public sealed class TestPlanningStore : IPlanningStore
{
    private readonly ConcurrentDictionary<StudyPlanId, StudyPlan> _plans = new();
    private readonly ConcurrentDictionary<SubjectId, Subject> _subjects = new();
    private readonly ConcurrentDictionary<TopicId, TopicNote> _notes = new();

    public bool FailNextSave { get; set; }

    public Task<StudyPlan?> GetStudyPlanByIdAsync(StudyPlanId planId, CancellationToken cancellationToken = default)
    {
        _plans.TryGetValue(planId, out var plan);
        return Task.FromResult(plan);
    }

    public Task<IReadOnlyList<StudyPlan>> ListStudyPlansAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StudyPlan> list = _plans.Values.ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<StudyPlanOptionView>> ListStudyPlanOptionsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StudyPlanOptionView> options =
        [
            .. _plans.Values
                .OrderBy(plan => plan.Name, StringComparer.Ordinal)
                .ThenBy(plan => plan.Id.Value)
                .Select(plan => new StudyPlanOptionView(plan.Id, plan.Name)),
        ];
        return Task.FromResult(options);
    }

    public Task AddStudyPlanAsync(StudyPlan plan, CancellationToken cancellationToken = default)
    {
        _plans[plan.Id] = plan;
        return Task.CompletedTask;
    }

    public Task RemoveStudyPlanAsync(StudyPlan plan, CancellationToken cancellationToken = default)
    {
        _plans.TryRemove(plan.Id, out _);
        return Task.CompletedTask;
    }

    public Task<Subject?> GetSubjectByIdAsync(SubjectId subjectId, CancellationToken cancellationToken = default)
    {
        _subjects.TryGetValue(subjectId, out var subject);
        return Task.FromResult(subject);
    }

    public Task<Subject?> ReloadSubjectByIdAsync(SubjectId subjectId, CancellationToken cancellationToken = default)
    {
        _subjects.TryGetValue(subjectId, out var subject);
        return Task.FromResult(subject);
    }

    public Task<IReadOnlyList<Subject>> ListSubjectsByPlanIdAsync(StudyPlanId planId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Subject> list = _subjects.Values.Where(s => s.StudyPlanId == planId).ToList();
        return Task.FromResult(list);
    }

    public Task AddSubjectAsync(Subject subject, CancellationToken cancellationToken = default)
    {
        _subjects[subject.Id] = subject;
        return Task.CompletedTask;
    }

    public Task RemoveSubjectAsync(Subject subject, CancellationToken cancellationToken = default)
    {
        _subjects.TryRemove(subject.Id, out _);
        return Task.CompletedTask;
    }

    public Task<TopicNote?> GetTopicNoteByTopicIdAsync(TopicId topicId, CancellationToken cancellationToken = default)
    {
        _notes.TryGetValue(topicId, out var note);
        return Task.FromResult(note);
    }

    public Task<IReadOnlyList<TopicNote>> ListTopicNotesByTopicIdsAsync(IReadOnlyCollection<TopicId> topicIds, CancellationToken cancellationToken = default)
    {
        var idSet = topicIds.ToHashSet();
        IReadOnlyList<TopicNote> list = _notes.Values.Where(n => idSet.Contains(n.TopicId)).ToList();
        return Task.FromResult(list);
    }

    public Task AddTopicNoteAsync(TopicNote note, CancellationToken cancellationToken = default)
    {
        _notes[note.TopicId] = note;
        return Task.CompletedTask;
    }

    public Task RemoveTopicNotesByTopicIdsAsync(IReadOnlyCollection<TopicId> topicIds, CancellationToken cancellationToken = default)
    {
        foreach (var id in topicIds)
        {
            _notes.TryRemove(id, out _);
        }
        return Task.CompletedTask;
    }

    public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (FailNextSave)
        {
            FailNextSave = false;
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public static void IncrementVersion(object aggregate)
    {
        var property = aggregate.GetType().GetProperty("Version", BindingFlags.Public | BindingFlags.Instance);
        if (property != null && property.PropertyType == typeof(int))
        {
            var current = (int)(property.GetValue(aggregate) ?? 0);
            var setter = property.GetSetMethod(true);
            setter?.Invoke(aggregate, [current + 1]);
        }
    }
}
