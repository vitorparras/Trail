using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Application.Tests.Planning;

public sealed class SectionHandlersTests
{
    private readonly TestCurrentUser _currentUser = new();
    private readonly FakePlanningStore _store = new();

    [Fact]
    public async Task AddSection_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new AddSectionHandler(_currentUser, _store);
        var command = new AddSectionCommand(StudyPlanId.New(), "Section", 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task AddSection_PlanNotFound_ReturnsNotFound()
    {
        var handler = new AddSectionHandler(_currentUser, _store);
        var command = new AddSectionCommand(StudyPlanId.New(), "Section", 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task AddSection_ConcurrencyMismatch_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new AddSectionHandler(_currentUser, _store);
        var command = new AddSectionCommand(plan.Id, "Section", plan.Version + 1);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task AddSection_DuplicateName_ReturnsInvalid()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        plan.AddSection("Existing");
        _store.Plans[plan.Id] = plan;
        var handler = new AddSectionHandler(_currentUser, _store);
        var command = new AddSectionCommand(plan.Id, "Existing", plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task AddSection_StoreConflict_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        _store.SaveChangesResult = false;
        var handler = new AddSectionHandler(_currentUser, _store);
        var command = new AddSectionCommand(plan.Id, "New Section", plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task AddSection_Valid_AddsSectionAndReturnsId()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new AddSectionHandler(_currentUser, _store);
        var command = new AddSectionCommand(plan.Id, "Module 1", plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var section = Assert.Single(plan.Sections);
        Assert.Equal(result.Data, section.Id);
        Assert.Equal("Module 1", section.Name);
    }

    [Fact]
    public async Task UpdateSection_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new UpdateSectionHandler(_currentUser, _store);
        var command = new UpdateSectionCommand(StudyPlanId.New(), SectionId.New(), "Renamed", 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task UpdateSection_PlanNotFound_ReturnsNotFound()
    {
        var handler = new UpdateSectionHandler(_currentUser, _store);
        var command = new UpdateSectionCommand(StudyPlanId.New(), SectionId.New(), "Renamed", 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateSection_ConcurrencyMismatch_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sec = plan.AddSection("Original").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new UpdateSectionHandler(_currentUser, _store);
        var command = new UpdateSectionCommand(plan.Id, sec.Id, "Renamed", plan.Version + 1);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task UpdateSection_SectionNotFound_ReturnsNotFound()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new UpdateSectionHandler(_currentUser, _store);
        var command = new UpdateSectionCommand(plan.Id, SectionId.New(), "Renamed", plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateSection_DuplicateName_ReturnsInvalid()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sec1 = plan.AddSection("First").Data;
        plan.AddSection("Second");
        _store.Plans[plan.Id] = plan;
        var handler = new UpdateSectionHandler(_currentUser, _store);
        var command = new UpdateSectionCommand(plan.Id, sec1.Id, "Second", plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task UpdateSection_StoreConflict_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sec = plan.AddSection("Original").Data;
        _store.Plans[plan.Id] = plan;
        _store.SaveChangesResult = false;
        var handler = new UpdateSectionHandler(_currentUser, _store);
        var command = new UpdateSectionCommand(plan.Id, sec.Id, "Renamed", plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task UpdateSection_Valid_RenamesSection()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sec = plan.AddSection("Old Name").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new UpdateSectionHandler(_currentUser, _store);
        var command = new UpdateSectionCommand(plan.Id, sec.Id, "New Name", plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Equal("New Name", sec.Name);
    }

    [Fact]
    public async Task ReorderSections_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new ReorderSectionsHandler(_currentUser, _store);
        var command = new ReorderSectionsCommand(StudyPlanId.New(), [], 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task ReorderSections_PlanNotFound_ReturnsNotFound()
    {
        var handler = new ReorderSectionsHandler(_currentUser, _store);
        var command = new ReorderSectionsCommand(StudyPlanId.New(), [], 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ReorderSections_ConcurrencyMismatch_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new ReorderSectionsHandler(_currentUser, _store);
        var command = new ReorderSectionsCommand(plan.Id, [], plan.Version + 1);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task ReorderSections_InvalidOrder_ReturnsInvalid()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sec1 = plan.AddSection("Sec 1").Data;
        plan.AddSection("Sec 2");
        _store.Plans[plan.Id] = plan;
        var handler = new ReorderSectionsHandler(_currentUser, _store);
        var command = new ReorderSectionsCommand(plan.Id, [sec1.Id], plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task ReorderSections_StoreConflict_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sec1 = plan.AddSection("Sec 1").Data;
        var sec2 = plan.AddSection("Sec 2").Data;
        _store.Plans[plan.Id] = plan;
        _store.SaveChangesResult = false;
        var handler = new ReorderSectionsHandler(_currentUser, _store);
        var command = new ReorderSectionsCommand(plan.Id, [sec2.Id, sec1.Id], plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task ReorderSections_Valid_UpdatesOrder()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sec1 = plan.AddSection("Sec 1").Data;
        var sec2 = plan.AddSection("Sec 2").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new ReorderSectionsHandler(_currentUser, _store);
        var command = new ReorderSectionsCommand(plan.Id, [sec2.Id, sec1.Id], plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var ordered = plan.Sections;
        Assert.Equal(sec2.Id, ordered[0].Id);
        Assert.Equal(sec1.Id, ordered[1].Id);
    }

    [Fact]
    public async Task DeleteSection_Unauthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = new DeleteSectionHandler(_currentUser, _store);
        var command = new DeleteSectionCommand(StudyPlanId.New(), SectionId.New(), 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task DeleteSection_PlanNotFound_ReturnsNotFound()
    {
        var handler = new DeleteSectionHandler(_currentUser, _store);
        var command = new DeleteSectionCommand(StudyPlanId.New(), SectionId.New(), 0);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteSection_ConcurrencyMismatch_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sec = plan.AddSection("Sec").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new DeleteSectionHandler(_currentUser, _store);
        var command = new DeleteSectionCommand(plan.Id, sec.Id, plan.Version + 1);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task DeleteSection_SectionNotFound_ReturnsNotFound()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        _store.Plans[plan.Id] = plan;
        var handler = new DeleteSectionHandler(_currentUser, _store);
        var command = new DeleteSectionCommand(plan.Id, SectionId.New(), plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteSection_StoreConflict_ReturnsConflict()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sec = plan.AddSection("Sec").Data;
        _store.Plans[plan.Id] = plan;
        _store.SaveChangesResult = false;
        var handler = new DeleteSectionHandler(_currentUser, _store);
        var command = new DeleteSectionCommand(plan.Id, sec.Id, plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task DeleteSection_Valid_RemovesSectionAndUnassignsSubject()
    {
        var plan = StudyPlan.Create(_currentUser.Id, "Plan").Data;
        var sec = plan.AddSection("Sec").Data;
        var subId = SubjectId.New();
        plan.PlaceSubject(subId, sec.Id);
        _store.Plans[plan.Id] = plan;

        var handler = new DeleteSectionHandler(_currentUser, _store);
        var command = new DeleteSectionCommand(plan.Id, sec.Id, plan.Version);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(plan.Sections);
        var placement = Assert.Single(plan.Outline);
        Assert.Null(placement.SectionId);
    }
}
