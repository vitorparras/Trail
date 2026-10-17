using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Trail.Application.Abstractions;
using Trail.Application.Planning;
using Trail.Application.Tests.Persistence;
using Trail.Domain.Common;
using Trail.Domain.Planning;
using Trail.Infrastructure.Persistence;
using Trail.Infrastructure.Persistence.Interceptors;

namespace Trail.Application.Tests.Planning;

public sealed class PlanningPersistenceTests
{
    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task FullLifecycle_CreatePlan_Sections_Subjects_HierarchicalTopics_Notes_And_Queries(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateHarnessAsync(provider, ct);

        var userA = UserId.New();
        using var scope = harness.CreateUserScope(userA);

        var createPlanHandler = scope.ServiceProvider.GetRequiredService<CreateStudyPlanHandler>();
        var addSectionHandler = scope.ServiceProvider.GetRequiredService<AddSectionHandler>();
        var createSubjectHandler = scope.ServiceProvider.GetRequiredService<CreateSubjectHandler>();
        var addTopicHandler = scope.ServiceProvider.GetRequiredService<AddTopicHandler>();
        var saveNoteHandler = scope.ServiceProvider.GetRequiredService<SaveTopicNoteHandler>();
        var getPlanHandler = scope.ServiceProvider.GetRequiredService<GetStudyPlanHandler>();
        var getSubjectTopicsHandler = scope.ServiceProvider.GetRequiredService<GetSubjectTopicsHandler>();

        var planCmd = new CreateStudyPlanCommand(
            Name: "Federal Police 2027",
            Description: "Preparation for Federal Police agent exam",
            ScoringRule: ScoringRule.Cebraspe,
            PeriodKind: PerformancePeriodKind.LastDays,
            PeriodDays: 60,
            LowBandThreshold: 50,
            HighBandThreshold: 75,
            ExamDate: new DateOnly(2027, 10, 20),
            DefaultMockExamQuestions: 120,
            FitnessTestEnabled: true);

        var planRes = await createPlanHandler.HandleAsync(planCmd, ct);
        Assert.True(planRes.IsOk);
        var planId = planRes.Data;

        var sec1Res = await addSectionHandler.HandleAsync(new AddSectionCommand(planId, "Basic Subjects", 0), ct);
        Assert.True(sec1Res.IsOk);
        var sec1Id = sec1Res.Data;

        var sec2Res = await addSectionHandler.HandleAsync(new AddSectionCommand(planId, "Specific Subjects", 1), ct);
        Assert.True(sec2Res.IsOk);
        var sec2Id = sec2Res.Data;

        var sub1Res = await createSubjectHandler.HandleAsync(new CreateSubjectCommand(planId, "Criminal Law", sec2Id, 2), ct);
        Assert.True(sub1Res.IsOk);
        var sub1Id = sub1Res.Data;

        var sub2Res = await createSubjectHandler.HandleAsync(new CreateSubjectCommand(planId, "Portuguese", sec1Id, 3), ct);
        Assert.True(sub2Res.IsOk);
        var sub2Id = sub2Res.Data;

        var t1Res = await addTopicHandler.HandleAsync(new AddTopicCommand(sub1Id, "Criminal Law Application", "1", null, TopicStatus.ToStudy, 0), ct);
        Assert.True(t1Res.IsOk);
        var t1Id = t1Res.Data;

        var t11Res = await addTopicHandler.HandleAsync(new AddTopicCommand(sub1Id, "Principle of Legality", "1.1", t1Id, TopicStatus.Studying, 1), ct);
        Assert.True(t11Res.IsOk);
        var t11Id = t11Res.Data;

        var t111Res = await addTopicHandler.HandleAsync(new AddTopicCommand(sub1Id, "Prior Law Requirement", "1.1.1", t11Id, TopicStatus.Studied, 2), ct);
        Assert.True(t111Res.IsOk);
        var t111Id = t111Res.Data;

        var t12Res = await addTopicHandler.HandleAsync(new AddTopicCommand(sub1Id, "Criminal Law in Space", "1.2", t1Id, TopicStatus.Reviewing, 3), ct);
        Assert.True(t12Res.IsOk);
        var t12Id = t12Res.Data;

        var note1Res = await saveNoteHandler.HandleAsync(new SaveTopicNoteCommand(sub1Id, t1Id, "Summary of Criminal Law Application", 0), ct);
        Assert.True(note1Res.IsOk);

        var note11Res = await saveNoteHandler.HandleAsync(new SaveTopicNoteCommand(sub1Id, t11Id, "Nullum crimen sine lege", 0), ct);
        Assert.True(note11Res.IsOk);

        var planDetailRes = await getPlanHandler.HandleAsync(new GetStudyPlanQuery(planId), ct);
        Assert.True(planDetailRes.IsOk);
        var planDetail = planDetailRes.Data;
        Assert.Equal("Federal Police 2027", planDetail.Name);
        Assert.Equal("Preparation for Federal Police agent exam", planDetail.Description);
        Assert.Equal(ScoringRule.Cebraspe, planDetail.ScoringRule);
        Assert.Equal(PerformancePeriodKind.LastDays, planDetail.DefaultPerformancePeriod.Kind);
        Assert.Equal(60, planDetail.DefaultPerformancePeriod.Days);
        Assert.Equal(50, planDetail.PerformanceBands.LowThreshold);
        Assert.Equal(75, planDetail.PerformanceBands.HighThreshold);
        Assert.Equal(new DateOnly(2027, 10, 20), planDetail.ExamDate);
        Assert.Equal(120, planDetail.DefaultMockExamQuestions);
        Assert.True(planDetail.FitnessTestEnabled);
        Assert.Equal(2, planDetail.Sections.Count);
        Assert.Equal(2, planDetail.Subjects.Count);
        Assert.Equal(4, planDetail.TotalTopicCount);
        Assert.Equal(1, planDetail.ToStudyCount);
        Assert.Equal(1, planDetail.StudyingCount);
        Assert.Equal(1, planDetail.StudiedCount);
        Assert.Equal(1, planDetail.ReviewingCount);

        var subjectDetailRes = await getSubjectTopicsHandler.HandleAsync(new GetSubjectTopicsQuery(sub1Id), ct);
        Assert.True(subjectDetailRes.IsOk);
        var subjectDetail = subjectDetailRes.Data;
        Assert.Equal("Criminal Law", subjectDetail.Name);
        Assert.Equal("Specific Subjects", subjectDetail.SectionName);
        Assert.Equal(4, subjectDetail.Topics.Count);

        var rootItem = subjectDetail.Topics.First(t => t.Id == t1Id);
        Assert.Equal(0, rootItem.Depth);
        Assert.True(rootItem.HasNote);
        Assert.Equal("1", rootItem.Code);

        var subItem = subjectDetail.Topics.First(t => t.Id == t11Id);
        Assert.Equal(1, subItem.Depth);
        Assert.True(subItem.HasNote);
        Assert.Equal("1.1", subItem.Code);

        var subSubItem = subjectDetail.Topics.First(t => t.Id == t111Id);
        Assert.Equal(2, subSubItem.Depth);
        Assert.False(subSubItem.HasNote);
        Assert.Equal("1.1.1", subSubItem.Code);

        var subItem2 = subjectDetail.Topics.First(t => t.Id == t12Id);
        Assert.Equal(1, subItem2.Depth);
        Assert.False(subItem2.HasNote);
        Assert.Equal("1.2", subItem2.Code);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task OptimisticConcurrency_TextEdit_StaleExpectedVersion_And_DbConcurrencyConflict(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateHarnessAsync(provider, ct);

        var user = UserId.New();
        StudyPlanId planId;
        SubjectId subjectId;
        TopicId topicId;

        using (var setupScope = harness.CreateUserScope(user))
        {
            var createPlanHandler = setupScope.ServiceProvider.GetRequiredService<CreateStudyPlanHandler>();
            var createSubjectHandler = setupScope.ServiceProvider.GetRequiredService<CreateSubjectHandler>();
            var addTopicHandler = setupScope.ServiceProvider.GetRequiredService<AddTopicHandler>();
            var saveNoteHandler = setupScope.ServiceProvider.GetRequiredService<SaveTopicNoteHandler>();

            var pRes = await createPlanHandler.HandleAsync(new CreateStudyPlanCommand("Concurrency Plan"), ct);
            Assert.True(pRes.IsOk);
            planId = pRes.Data;

            var sRes = await createSubjectHandler.HandleAsync(new CreateSubjectCommand(planId, "Concurrency Subject"), ct);
            Assert.True(sRes.IsOk);
            subjectId = sRes.Data;

            var tRes = await addTopicHandler.HandleAsync(new AddTopicCommand(subjectId, "Concurrency Topic", "1"), ct);
            Assert.True(tRes.IsOk);
            topicId = tRes.Data;

            var nRes = await saveNoteHandler.HandleAsync(new SaveTopicNoteCommand(subjectId, topicId, "Initial Note Content", 0), ct);
            Assert.True(nRes.IsOk);
        }

        using (var testScope = harness.CreateUserScope(user))
        {
            var updatePlanHandler = testScope.ServiceProvider.GetRequiredService<UpdateStudyPlanHandler>();
            var updateTopicHandler = testScope.ServiceProvider.GetRequiredService<UpdateTopicHandler>();
            var saveNoteHandler = testScope.ServiceProvider.GetRequiredService<SaveTopicNoteHandler>();

            var stalePlanRes = await updatePlanHandler.HandleAsync(
                new UpdateStudyPlanCommand(planId, ExpectedVersion: 999, Name: "Stale Plan Name"), ct);
            Assert.False(stalePlanRes.IsOk);
            Assert.Equal(ResultStatus.Conflict, stalePlanRes.Status);

            var staleTopicRes = await updateTopicHandler.HandleAsync(
                new UpdateTopicCommand(subjectId, topicId, "Stale Topic Name", "1", ExpectedSubjectVersion: 999), ct);
            Assert.False(staleTopicRes.IsOk);
            Assert.Equal(ResultStatus.Conflict, staleTopicRes.Status);

            var staleNoteRes = await saveNoteHandler.HandleAsync(
                new SaveTopicNoteCommand(subjectId, topicId, "Stale Note Markdown", ExpectedVersion: 999), ct);
            Assert.False(staleNoteRes.IsOk);
            Assert.Equal(ResultStatus.Conflict, staleNoteRes.Status);
        }

        using var scope1 = harness.CreateUserScope(user);
        using var scope2 = harness.CreateUserScope(user);

        var store1 = scope1.ServiceProvider.GetRequiredService<IPlanningStore>();
        var store2 = scope2.ServiceProvider.GetRequiredService<IPlanningStore>();

        var plan1 = await store1.GetStudyPlanByIdAsync(planId, ct);
        var plan2 = await store2.GetStudyPlanByIdAsync(planId, ct);
        Assert.NotNull(plan1);
        Assert.NotNull(plan2);

        plan1.Update("Updated by Context 1", plan1.Description, plan1.ScoringRule, plan1.DefaultPerformancePeriod, plan1.PerformanceBands, plan1.ExamDate, plan1.DefaultMockExamQuestions, plan1.FitnessTestEnabled);
        var planSaved1 = await store1.TrySaveChangesAsync(ct);
        Assert.True(planSaved1);

        plan2.Update("Updated by Context 2", plan2.Description, plan2.ScoringRule, plan2.DefaultPerformancePeriod, plan2.PerformanceBands, plan2.ExamDate, plan2.DefaultMockExamQuestions, plan2.FitnessTestEnabled);
        var planSaved2 = await store2.TrySaveChangesAsync(ct);
        Assert.False(planSaved2);

        var note1 = await store1.GetTopicNoteByTopicIdAsync(topicId, ct);
        var note2 = await store2.GetTopicNoteByTopicIdAsync(topicId, ct);
        Assert.NotNull(note1);
        Assert.NotNull(note2);

        note1.Update("Context 1 Note Edit");
        var noteSaved1 = await store1.TrySaveChangesAsync(ct);
        Assert.True(noteSaved1);

        note2.Update("Context 2 Note Edit");
        var noteSaved2 = await store2.TrySaveChangesAsync(ct);
        Assert.False(noteSaved2);

        var sub1 = await store1.GetSubjectByIdAsync(subjectId, ct);
        var sub2 = await store2.GetSubjectByIdAsync(subjectId, ct);
        Assert.NotNull(sub1);
        Assert.NotNull(sub2);

        sub1.Rename("Subject Context 1");
        var subSaved1 = await store1.TrySaveChangesAsync(ct);
        Assert.True(subSaved1);

        sub2.Rename("Subject Context 2");
        var subSaved2 = await store2.TrySaveChangesAsync(ct);
        Assert.False(subSaved2);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task ChangeTopicStatus_ConcurrentModification_RetriesSuccessfullyWithoutDataLoss(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateHarnessAsync(provider, ct);

        var user = UserId.New();
        StudyPlanId planId;
        SubjectId subjectId;
        TopicId topic1Id;

        using (var setupScope = harness.CreateUserScope(user))
        {
            var createPlanHandler = setupScope.ServiceProvider.GetRequiredService<CreateStudyPlanHandler>();
            var createSubjectHandler = setupScope.ServiceProvider.GetRequiredService<CreateSubjectHandler>();
            var addTopicHandler = setupScope.ServiceProvider.GetRequiredService<AddTopicHandler>();

            var pRes = await createPlanHandler.HandleAsync(new CreateStudyPlanCommand("Status Retry Plan"), ct);
            Assert.True(pRes.IsOk);
            planId = pRes.Data;

            var sRes = await createSubjectHandler.HandleAsync(new CreateSubjectCommand(planId, "Status Retry Subject"), ct);
            Assert.True(sRes.IsOk);
            subjectId = sRes.Data;

            var t1Res = await addTopicHandler.HandleAsync(new AddTopicCommand(subjectId, "Topic 1", "1", null, TopicStatus.ToStudy), ct);
            Assert.True(t1Res.IsOk);
            topic1Id = t1Res.Data;
        }

        using var scope1 = harness.CreateUserScope(user);
        using var scope2 = harness.CreateUserScope(user);

        var store1 = scope1.ServiceProvider.GetRequiredService<IPlanningStore>();
        var changeHandler = scope1.ServiceProvider.GetRequiredService<ChangeTopicStatusHandler>();

        var preloadedSubject1 = await store1.GetSubjectByIdAsync(subjectId, ct);
        Assert.NotNull(preloadedSubject1);
        var initialVersion = preloadedSubject1.Version;

        var store2 = scope2.ServiceProvider.GetRequiredService<IPlanningStore>();
        var subject2 = await store2.GetSubjectByIdAsync(subjectId, ct);
        Assert.NotNull(subject2);
        var addTopic2Res = subject2.AddTopic("Topic 2", null, null, TopicStatus.ToStudy);
        Assert.True(addTopic2Res.IsOk);
        var savedScope2 = await store2.TrySaveChangesAsync(ct);
        Assert.True(savedScope2);

        var statusResult = await changeHandler.HandleAsync(
            new ChangeTopicStatusCommand(subjectId, topic1Id, TopicStatus.Studying), ct);
        Assert.True(statusResult.IsOk);

        using var verifyScope = harness.CreateUserScope(user);
        var verifyStore = verifyScope.ServiceProvider.GetRequiredService<IPlanningStore>();
        var finalSubject = await verifyStore.GetSubjectByIdAsync(subjectId, ct);

        Assert.NotNull(finalSubject);
        Assert.Equal(initialVersion + 2, finalSubject.Version);
        Assert.Equal(2, finalSubject.Topics.Count);

        var finalT1 = finalSubject.Topics.First(t => t.Id == topic1Id);
        Assert.Equal(TopicStatus.Studying, finalT1.Status);

        var finalT2 = finalSubject.Topics.First(t => t.Id != topic1Id);
        Assert.Equal("Topic 2", finalT2.Title);
        Assert.Equal(TopicStatus.ToStudy, finalT2.Status);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task CascadeDeletion_TopicsSubtopicsNotesSubjectsAndPlan_RemovesRelatedEntities(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateHarnessAsync(provider, ct);

        var user = UserId.New();
        StudyPlanId planId;
        SectionId sectionId;
        SubjectId subject1Id;
        SubjectId subject2Id;
        TopicId t1Id;
        TopicId t11Id;
        TopicId t111Id;
        TopicId t12Id;
        TopicId t2Id;
        TopicId t3Id;

        using (var setupScope = harness.CreateUserScope(user))
        {
            var createPlanHandler = setupScope.ServiceProvider.GetRequiredService<CreateStudyPlanHandler>();
            var addSectionHandler = setupScope.ServiceProvider.GetRequiredService<AddSectionHandler>();
            var createSubjectHandler = setupScope.ServiceProvider.GetRequiredService<CreateSubjectHandler>();
            var addTopicHandler = setupScope.ServiceProvider.GetRequiredService<AddTopicHandler>();
            var saveNoteHandler = setupScope.ServiceProvider.GetRequiredService<SaveTopicNoteHandler>();

            var pRes = await createPlanHandler.HandleAsync(new CreateStudyPlanCommand("Cascade Plan"), ct);
            planId = pRes.Data;

            var secRes = await addSectionHandler.HandleAsync(new AddSectionCommand(planId, "Main Section", 0), ct);
            sectionId = secRes.Data;

            var s1Res = await createSubjectHandler.HandleAsync(new CreateSubjectCommand(planId, "Subject 1", sectionId, 1), ct);
            subject1Id = s1Res.Data;

            var s2Res = await createSubjectHandler.HandleAsync(new CreateSubjectCommand(planId, "Subject 2", sectionId, 2), ct);
            subject2Id = s2Res.Data;

            var t1Res = await addTopicHandler.HandleAsync(new AddTopicCommand(subject1Id, "Topic 1", "1"), ct);
            t1Id = t1Res.Data;
            var t11Res = await addTopicHandler.HandleAsync(new AddTopicCommand(subject1Id, "Topic 1.1", "1.1", t1Id), ct);
            t11Id = t11Res.Data;
            var t111Res = await addTopicHandler.HandleAsync(new AddTopicCommand(subject1Id, "Topic 1.1.1", "1.1.1", t11Id), ct);
            t111Id = t111Res.Data;
            var t12Res = await addTopicHandler.HandleAsync(new AddTopicCommand(subject1Id, "Topic 1.2", "1.2", t1Id), ct);
            t12Id = t12Res.Data;
            var t2Res = await addTopicHandler.HandleAsync(new AddTopicCommand(subject1Id, "Topic 2", "2"), ct);
            t2Id = t2Res.Data;

            var t3Res = await addTopicHandler.HandleAsync(new AddTopicCommand(subject2Id, "Topic 3", "3"), ct);
            t3Id = t3Res.Data;

            await saveNoteHandler.HandleAsync(new SaveTopicNoteCommand(subject1Id, t1Id, "Note 1", 0), ct);
            await saveNoteHandler.HandleAsync(new SaveTopicNoteCommand(subject1Id, t11Id, "Note 1.1", 0), ct);
            await saveNoteHandler.HandleAsync(new SaveTopicNoteCommand(subject1Id, t111Id, "Note 1.1.1", 0), ct);
            await saveNoteHandler.HandleAsync(new SaveTopicNoteCommand(subject1Id, t12Id, "Note 1.2", 0), ct);
            await saveNoteHandler.HandleAsync(new SaveTopicNoteCommand(subject1Id, t2Id, "Note 2", 0), ct);
            await saveNoteHandler.HandleAsync(new SaveTopicNoteCommand(subject2Id, t3Id, "Note 3", 0), ct);
        }

        using var deleteScope = harness.CreateUserScope(user);

        var impactHandler = deleteScope.ServiceProvider.GetRequiredService<GetDeletionImpactHandler>();
        var deleteTopicHandler = deleteScope.ServiceProvider.GetRequiredService<DeleteTopicHandler>();
        var deleteSubjectHandler = deleteScope.ServiceProvider.GetRequiredService<DeleteSubjectHandler>();
        var deletePlanHandler = deleteScope.ServiceProvider.GetRequiredService<DeleteStudyPlanHandler>();
        var store = deleteScope.ServiceProvider.GetRequiredService<IPlanningStore>();

        var topicImpact = await impactHandler.HandleAsync(
            new GetDeletionImpactQuery(DeletionTargetKind.Topic, planId, null, subject1Id, t1Id), ct);
        Assert.True(topicImpact.IsOk);
        Assert.Equal(4, topicImpact.Data.TopicCount);
        Assert.Equal(4, topicImpact.Data.NoteCount);

        var delTopicResult = await deleteTopicHandler.HandleAsync(new DeleteTopicCommand(subject1Id, t1Id), ct);
        Assert.True(delTopicResult.IsOk);

        var s1AfterTopicDel = await store.GetSubjectByIdAsync(subject1Id, ct);
        Assert.NotNull(s1AfterTopicDel);
        Assert.Single(s1AfterTopicDel.Topics);
        Assert.Equal(t2Id, s1AfterTopicDel.Topics[0].Id);

        var remainingSub1Notes = await store.ListTopicNotesByTopicIdsAsync([t1Id, t11Id, t111Id, t12Id, t2Id], ct);
        Assert.Single(remainingSub1Notes);
        Assert.Equal(t2Id, remainingSub1Notes[0].TopicId);

        var subjectImpact = await impactHandler.HandleAsync(
            new GetDeletionImpactQuery(DeletionTargetKind.Subject, planId, null, subject1Id), ct);
        Assert.True(subjectImpact.IsOk);
        Assert.Equal(1, subjectImpact.Data.TopicCount);
        Assert.Equal(1, subjectImpact.Data.NoteCount);

        var delSubResult = await deleteSubjectHandler.HandleAsync(new DeleteSubjectCommand(planId, subject1Id), ct);
        Assert.True(delSubResult.IsOk);

        var s1AfterSubDel = await store.GetSubjectByIdAsync(subject1Id, ct);
        Assert.Null(s1AfterSubDel);

        var note2AfterSubDel = await store.GetTopicNoteByTopicIdAsync(t2Id, ct);
        Assert.Null(note2AfterSubDel);

        var planAfterSubDel = await store.GetStudyPlanByIdAsync(planId, ct);
        Assert.NotNull(planAfterSubDel);
        Assert.Single(planAfterSubDel.Outline);
        Assert.Equal(subject2Id, planAfterSubDel.Outline[0].SubjectId);

        var planImpact = await impactHandler.HandleAsync(
            new GetDeletionImpactQuery(DeletionTargetKind.StudyPlan, planId), ct);
        Assert.True(planImpact.IsOk);
        Assert.Equal(1, planImpact.Data.SectionCount);
        Assert.Equal(1, planImpact.Data.SubjectCount);
        Assert.Equal(1, planImpact.Data.TopicCount);
        Assert.Equal(1, planImpact.Data.NoteCount);

        var delPlanResult = await deletePlanHandler.HandleAsync(new DeleteStudyPlanCommand(planId), ct);
        Assert.True(delPlanResult.IsOk);

        var planAfterPlanDel = await store.GetStudyPlanByIdAsync(planId, ct);
        Assert.Null(planAfterPlanDel);

        var s2AfterPlanDel = await store.GetSubjectByIdAsync(subject2Id, ct);
        Assert.Null(s2AfterPlanDel);

        var note3AfterPlanDel = await store.GetTopicNoteByTopicIdAsync(t3Id, ct);
        Assert.Null(note3AfterPlanDel);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task AccentInsensitiveSearch_FindsNormalizedTopicsOnBothProviders(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateHarnessAsync(provider, ct);

        var user = UserId.New();
        using var scope = harness.CreateUserScope(user);

        var createPlanHandler = scope.ServiceProvider.GetRequiredService<CreateStudyPlanHandler>();
        var createSubjectHandler = scope.ServiceProvider.GetRequiredService<CreateSubjectHandler>();
        var addTopicHandler = scope.ServiceProvider.GetRequiredService<AddTopicHandler>();
        var searchHandler = scope.ServiceProvider.GetRequiredService<SearchPlanTopicsHandler>();

        var pRes = await createPlanHandler.HandleAsync(new CreateStudyPlanCommand("Search Plan"), ct);
        var planId = pRes.Data;

        var sRes = await createSubjectHandler.HandleAsync(new CreateSubjectCommand(planId, "Computer Science"), ct);
        var subjectId = sRes.Data;

        await addTopicHandler.HandleAsync(new AddTopicCommand(subjectId, "Árvore de Decisão", "1.1"), ct);
        await addTopicHandler.HandleAsync(new AddTopicCommand(subjectId, "Programação Dinâmica", "1.2"), ct);
        await addTopicHandler.HandleAsync(new AddTopicCommand(subjectId, "Criptografia Simétrica", "1.3"), ct);
        await addTopicHandler.HandleAsync(new AddTopicCommand(subjectId, "Redes de Computadores", "1.4"), ct);

        var queryArvore = await searchHandler.HandleAsync(new SearchPlanTopicsQuery(planId, SearchQuery: "arvore"), ct);
        Assert.True(queryArvore.IsOk);
        Assert.Single(queryArvore.Data);
        Assert.Equal("Árvore de Decisão", queryArvore.Data[0].Title);

        var queryProgramacao = await searchHandler.HandleAsync(new SearchPlanTopicsQuery(planId, SearchQuery: "programacao"), ct);
        Assert.True(queryProgramacao.IsOk);
        Assert.Single(queryProgramacao.Data);
        Assert.Equal("Programação Dinâmica", queryProgramacao.Data[0].Title);

        var querySimetrica = await searchHandler.HandleAsync(new SearchPlanTopicsQuery(planId, SearchQuery: "simetrica"), ct);
        Assert.True(querySimetrica.IsOk);
        Assert.Single(querySimetrica.Data);
        Assert.Equal("Criptografia Simétrica", querySimetrica.Data[0].Title);

        var queryAccented = await searchHandler.HandleAsync(new SearchPlanTopicsQuery(planId, SearchQuery: "árvore"), ct);
        Assert.True(queryAccented.IsOk);
        Assert.Single(queryAccented.Data);
        Assert.Equal("Árvore de Decisão", queryAccented.Data[0].Title);

        var queryCode = await searchHandler.HandleAsync(new SearchPlanTopicsQuery(planId, SearchQuery: "1.3"), ct);
        Assert.True(queryCode.IsOk);
        Assert.Single(queryCode.Data);
        Assert.Equal("Criptografia Simétrica", queryCode.Data[0].Title);

        var queryComputadores = await searchHandler.HandleAsync(new SearchPlanTopicsQuery(planId, SearchQuery: "computadores"), ct);
        Assert.True(queryComputadores.IsOk);
        Assert.Single(queryComputadores.Data);
        Assert.Equal("Redes de Computadores", queryComputadores.Data[0].Title);
    }

    [Theory]
    [MemberData(nameof(TestDatabaseProviders.EnabledProviders), MemberType = typeof(TestDatabaseProviders))]
    public async Task OwnerIsolation_UserCannotReadOrModifyAnotherUsersData(DatabaseProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await CreateHarnessAsync(provider, ct);

        var userA = UserId.New();
        var userB = UserId.New();

        StudyPlanId planId;
        SubjectId subjectId;
        TopicId topicId;

        using (var scopeA = harness.CreateUserScope(userA))
        {
            var createPlanHandler = scopeA.ServiceProvider.GetRequiredService<CreateStudyPlanHandler>();
            var addSectionHandler = scopeA.ServiceProvider.GetRequiredService<AddSectionHandler>();
            var createSubjectHandler = scopeA.ServiceProvider.GetRequiredService<CreateSubjectHandler>();
            var addTopicHandler = scopeA.ServiceProvider.GetRequiredService<AddTopicHandler>();
            var saveNoteHandler = scopeA.ServiceProvider.GetRequiredService<SaveTopicNoteHandler>();

            var pRes = await createPlanHandler.HandleAsync(new CreateStudyPlanCommand("User A Plan"), ct);
            planId = pRes.Data;

            var secRes = await addSectionHandler.HandleAsync(new AddSectionCommand(planId, "Section A", 0), ct);
            var sectionId = secRes.Data;

            var sRes = await createSubjectHandler.HandleAsync(new CreateSubjectCommand(planId, "Subject A", sectionId, 1), ct);
            subjectId = sRes.Data;

            var tRes = await addTopicHandler.HandleAsync(new AddTopicCommand(subjectId, "Topic A", "1"), ct);
            topicId = tRes.Data;

            await saveNoteHandler.HandleAsync(new SaveTopicNoteCommand(subjectId, topicId, "Note A", 0), ct);
        }

        using var scopeB = harness.CreateUserScope(userB);

        var getPlanHandlerB = scopeB.ServiceProvider.GetRequiredService<GetStudyPlanHandler>();
        var getSubjectTopicsHandlerB = scopeB.ServiceProvider.GetRequiredService<GetSubjectTopicsHandler>();
        var getNoteHandlerB = scopeB.ServiceProvider.GetRequiredService<GetTopicNoteHandler>();
        var updatePlanHandlerB = scopeB.ServiceProvider.GetRequiredService<UpdateStudyPlanHandler>();
        var updateTopicHandlerB = scopeB.ServiceProvider.GetRequiredService<UpdateTopicHandler>();
        var saveNoteHandlerB = scopeB.ServiceProvider.GetRequiredService<SaveTopicNoteHandler>();
        var deleteTopicHandlerB = scopeB.ServiceProvider.GetRequiredService<DeleteTopicHandler>();
        var deleteSubjectHandlerB = scopeB.ServiceProvider.GetRequiredService<DeleteSubjectHandler>();
        var deletePlanHandlerB = scopeB.ServiceProvider.GetRequiredService<DeleteStudyPlanHandler>();

        var planRes = await getPlanHandlerB.HandleAsync(new GetStudyPlanQuery(planId), ct);
        Assert.False(planRes.IsOk);
        Assert.Equal(ResultStatus.NotFound, planRes.Status);

        var subjectRes = await getSubjectTopicsHandlerB.HandleAsync(new GetSubjectTopicsQuery(subjectId), ct);
        Assert.False(subjectRes.IsOk);
        Assert.Equal(ResultStatus.NotFound, subjectRes.Status);

        var noteRes = await getNoteHandlerB.HandleAsync(new GetTopicNoteQuery(subjectId, topicId), ct);
        Assert.False(noteRes.IsOk);
        Assert.Equal(ResultStatus.NotFound, noteRes.Status);

        var updatePlanRes = await updatePlanHandlerB.HandleAsync(new UpdateStudyPlanCommand(planId, 0, "Hijacked Plan"), ct);
        Assert.False(updatePlanRes.IsOk);
        Assert.Equal(ResultStatus.NotFound, updatePlanRes.Status);

        var updateTopicRes = await updateTopicHandlerB.HandleAsync(new UpdateTopicCommand(subjectId, topicId, "Hijacked Topic", null, 0), ct);
        Assert.False(updateTopicRes.IsOk);
        Assert.Equal(ResultStatus.NotFound, updateTopicRes.Status);

        var saveNoteRes = await saveNoteHandlerB.HandleAsync(new SaveTopicNoteCommand(subjectId, topicId, "Hijacked Note", 0), ct);
        Assert.False(saveNoteRes.IsOk);
        Assert.Equal(ResultStatus.NotFound, saveNoteRes.Status);

        var delTopicRes = await deleteTopicHandlerB.HandleAsync(new DeleteTopicCommand(subjectId, topicId), ct);
        Assert.False(delTopicRes.IsOk);
        Assert.Equal(ResultStatus.NotFound, delTopicRes.Status);

        var delSubRes = await deleteSubjectHandlerB.HandleAsync(new DeleteSubjectCommand(planId, subjectId), ct);
        Assert.False(delSubRes.IsOk);
        Assert.Equal(ResultStatus.NotFound, delSubRes.Status);

        var delPlanRes = await deletePlanHandlerB.HandleAsync(new DeleteStudyPlanCommand(planId), ct);
        Assert.False(delPlanRes.IsOk);
        Assert.Equal(ResultStatus.NotFound, delPlanRes.Status);
    }

    private static async Task<PlanningTestHarness> CreateHarnessAsync(
        DatabaseProvider provider,
        CancellationToken cancellationToken = default)
    {
        DbConnection? sqliteConnection = null;
        string? postgresSchema = null;
        string? postgresConnectionString = null;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<DomainEventDispatchInterceptor>();

        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());

        if (provider == DatabaseProvider.Sqlite)
        {
            sqliteConnection = new SqliteConnection("Data Source=:memory:");
            await sqliteConnection.OpenAsync(cancellationToken);
            services.AddDbContext<AppDbContext>((sp, opt) =>
            {
                var interceptor = sp.GetRequiredService<DomainEventDispatchInterceptor>();
                opt.UseSqlite(sqliteConnection).AddInterceptors(interceptor);
            });
        }
        else
        {
            var baseConnectionString = await PostgreSqlTestDatabase.GetConnectionStringAsync(cancellationToken);
            postgresSchema = $"test_plan_{Guid.NewGuid():N}";
            await using (var connection = new NpgsqlConnection(baseConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE SCHEMA \"{postgresSchema}\";";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var csb = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                SearchPath = postgresSchema,
            };
            postgresConnectionString = baseConnectionString;
            services.AddDbContext<AppDbContext>((sp, opt) =>
            {
                var interceptor = sp.GetRequiredService<DomainEventDispatchInterceptor>();
                opt.UseNpgsql(csb.ConnectionString).AddInterceptors(interceptor);
            });
        }

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddApplication();
        services.Replace(ServiceDescriptor.Scoped<IPlanningStore, EfPlanningStore>());

        var rootProvider = services.BuildServiceProvider();

        using (var initScope = rootProvider.CreateScope())
        {
            var dbContext = initScope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (provider == DatabaseProvider.Sqlite)
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            }
            else
            {
                var script = dbContext.Database.GenerateCreateScript();
                await dbContext.Database.ExecuteSqlRawAsync(script, cancellationToken);
            }
        }

        return new PlanningTestHarness(rootProvider, sqliteConnection, postgresSchema, postgresConnectionString);
    }

    private sealed class PlanningTestHarness(
        ServiceProvider rootProvider,
        DbConnection? sqliteConnection,
        string? postgresSchema,
        string? postgresConnectionString) : IAsyncDisposable
    {
        public IServiceScope CreateUserScope(UserId userId)
        {
            var scope = rootProvider.CreateScope();
            var user = scope.ServiceProvider.GetRequiredService<TestCurrentUser>();
            user.Id = userId;
            user.IsAuthenticated = true;
            return scope;
        }

        public async ValueTask DisposeAsync()
        {
            await rootProvider.DisposeAsync();

            if (sqliteConnection is not null)
            {
                await sqliteConnection.DisposeAsync();
            }

            if (postgresSchema is not null && postgresConnectionString is not null)
            {
                await using var connection = new NpgsqlConnection(postgresConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP SCHEMA IF EXISTS \"{postgresSchema}\" CASCADE;";
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
