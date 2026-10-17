using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Tests.Planning;

public sealed class FallbackPlanningStoreTests
{
    [Fact]
    public async Task FallbackPlanningStore_AllMethods_ExecuteSafely()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FallbackPlanningStore();
        var planId = StudyPlanId.New();
        var subjectId = SubjectId.New();
        var topicId = TopicId.New();
        var plan = StudyPlan.Create(UserId.New(), "Plan").Data;
        var subject = Subject.Create(UserId.New(), planId, "Subject").Data;
        var note = TopicNote.Create(UserId.New(), topicId, "Content").Data;

        Assert.Null(await store.GetStudyPlanByIdAsync(planId, ct));
        Assert.Empty(await store.ListStudyPlansAsync(ct));
        await store.AddStudyPlanAsync(plan, ct);
        await store.RemoveStudyPlanAsync(plan, ct);

        Assert.Null(await store.GetSubjectByIdAsync(subjectId, ct));
        Assert.Null(await store.ReloadSubjectByIdAsync(subjectId, ct));
        Assert.Empty(await store.ListSubjectsByPlanIdAsync(planId, ct));
        await store.AddSubjectAsync(subject, ct);
        await store.RemoveSubjectAsync(subject, ct);

        Assert.Null(await store.GetTopicNoteByTopicIdAsync(topicId, ct));
        Assert.Empty(await store.ListTopicNotesByTopicIdsAsync([topicId], ct));
        await store.AddTopicNoteAsync(note, ct);
        await store.RemoveTopicNotesByTopicIdsAsync([topicId], ct);

        Assert.True(await store.TrySaveChangesAsync(ct));
    }
}
