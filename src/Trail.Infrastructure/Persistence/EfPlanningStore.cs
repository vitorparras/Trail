using Microsoft.EntityFrameworkCore;
using Trail.Application.Planning;
using Trail.Domain.Planning;

namespace Trail.Infrastructure.Persistence;

public sealed class EfPlanningStore : IPlanningStore
{
    private readonly AppDbContext _dbContext;

    public EfPlanningStore(AppDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<StudyPlan?> GetStudyPlanByIdAsync(StudyPlanId planId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.StudyPlans
            .Include(p => p.Sections)
            .Include(p => p.Outline)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);
    }

    public async Task<IReadOnlyList<StudyPlan>> ListStudyPlansAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.StudyPlans
            .Include(p => p.Sections)
            .Include(p => p.Outline)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddStudyPlanAsync(StudyPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        await _dbContext.StudyPlans.AddAsync(plan, cancellationToken);
    }

    public Task RemoveStudyPlanAsync(StudyPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        _dbContext.StudyPlans.Remove(plan);
        return Task.CompletedTask;
    }

    public async Task<Subject?> GetSubjectByIdAsync(SubjectId subjectId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Subjects
            .Include(s => s.Topics)
            .FirstOrDefaultAsync(s => s.Id == subjectId, cancellationToken);
    }

    public async Task<Subject?> ReloadSubjectByIdAsync(SubjectId subjectId, CancellationToken cancellationToken = default)
    {
        var trackedSubjects = _dbContext.ChangeTracker.Entries<Subject>()
            .Where(e => e.Entity.Id == subjectId)
            .ToList();

        foreach (var entry in trackedSubjects)
        {
            var topicEntries = _dbContext.ChangeTracker.Entries<Topic>()
                .Where(t => t.Entity.SubjectId == subjectId)
                .ToList();

            foreach (var topicEntry in topicEntries)
            {
                topicEntry.State = EntityState.Detached;
            }

            entry.State = EntityState.Detached;
        }

        return await _dbContext.Subjects
            .Include(s => s.Topics)
            .FirstOrDefaultAsync(s => s.Id == subjectId, cancellationToken);
    }

    public async Task<IReadOnlyList<Subject>> ListSubjectsByPlanIdAsync(StudyPlanId planId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Subjects
            .Include(s => s.Topics)
            .Where(s => s.StudyPlanId == planId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddSubjectAsync(Subject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        await _dbContext.Subjects.AddAsync(subject, cancellationToken);
    }

    public Task RemoveSubjectAsync(Subject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        _dbContext.Subjects.Remove(subject);
        return Task.CompletedTask;
    }

    public async Task<TopicNote?> GetTopicNoteByTopicIdAsync(TopicId topicId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TopicNotes
            .FirstOrDefaultAsync(n => n.TopicId == topicId, cancellationToken);
    }

    public async Task<IReadOnlyList<TopicNote>> ListTopicNotesByTopicIdsAsync(IReadOnlyCollection<TopicId> topicIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topicIds);

        if (topicIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.TopicNotes
            .Where(n => topicIds.Contains(n.TopicId))
            .ToListAsync(cancellationToken);
    }

    public async Task AddTopicNoteAsync(TopicNote note, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(note);
        await _dbContext.TopicNotes.AddAsync(note, cancellationToken);
    }

    public async Task RemoveTopicNotesByTopicIdsAsync(IReadOnlyCollection<TopicId> topicIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topicIds);

        if (topicIds.Count == 0)
        {
            return;
        }

        var notes = await _dbContext.TopicNotes
            .Where(n => topicIds.Contains(n.TopicId))
            .ToListAsync(cancellationToken);

        var localNotes = _dbContext.ChangeTracker.Entries<TopicNote>()
            .Where(e => topicIds.Contains(e.Entity.TopicId))
            .Select(e => e.Entity)
            .ToList();

        var allNotes = notes.Union(localNotes).ToList();

        if (allNotes.Count > 0)
        {
            _dbContext.TopicNotes.RemoveRange(allNotes);
        }
    }

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
