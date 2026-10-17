using System.Reflection;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Domain.Tests.Planning;

public sealed class StudyPlanTests
{
    private readonly UserId _ownerId = UserId.New();

    [Fact]
    public void Create_WithValidData_ReturnsPlanAndRaisesStudyPlanCreated()
    {
        var examDate = new DateOnly(2027, 5, 20);
        var period = PerformancePeriod.LastDays(30).Data;
        var bands = PerformanceBands.Create(40, 70, ScoringRule.Cebraspe).Data;

        var result = StudyPlan.Create(
            _ownerId,
            "Perito Criminal Federal",
            "Trilha de estudo focada em TI",
            ScoringRule.Cebraspe,
            period,
            bands,
            examDate,
            defaultMockExamQuestions: 120,
            fitnessTestEnabled: true,
            origin: StudyPlanOrigin.Manual);

        Assert.True(result.IsOk);
        var plan = result.Data;
        Assert.Equal("Perito Criminal Federal", plan.Name);
        Assert.Equal("Trilha de estudo focada em TI", plan.Description);
        Assert.Equal(ScoringRule.Cebraspe, plan.ScoringRule);
        Assert.Equal(period, plan.DefaultPerformancePeriod);
        Assert.Equal(bands, plan.PerformanceBands);
        Assert.Equal(examDate, plan.ExamDate);
        Assert.Equal(120, plan.DefaultMockExamQuestions);
        Assert.True(plan.FitnessTestEnabled);

        var events = plan.PullDomainEvents();
        var createdEvent = Assert.Single(events);
        var planCreated = Assert.IsType<StudyPlanCreated>(createdEvent);
        Assert.Equal(plan.Id, planCreated.StudyPlanId);
        Assert.Equal(_ownerId, planCreated.OwnerId);
        Assert.Equal(StudyPlanOrigin.Manual, planCreated.Origin);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ReturnsInvalid(string? name)
    {
        var result = StudyPlan.Create(_ownerId, name);

        Assert.False(result.IsOk);
        Assert.Equal("planning.plan.name_required", result.Failure.Messages[0].Code);
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_ReturnsInvalid()
    {
        var longName = new string('a', StudyPlan.MaxNameLength + 1);
        var result = StudyPlan.Create(_ownerId, longName);

        Assert.False(result.IsOk);
        Assert.Equal("planning.plan.name_too_long", result.Failure.Messages[0].Code);
    }

    [Fact]
    public void Create_WithDescriptionExceedingMaxLength_ReturnsInvalid()
    {
        var longDescription = new string('d', StudyPlan.MaxDescriptionLength + 1);
        var result = StudyPlan.Create(_ownerId, "Valid Name", longDescription);

        Assert.False(result.IsOk);
        Assert.Equal("planning.plan.description_too_long", result.Failure.Messages[0].Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_WithNonPositiveMockQuestions_ReturnsInvalid(int questions)
    {
        var result = StudyPlan.Create(_ownerId, "Plan", defaultMockExamQuestions: questions);

        Assert.False(result.IsOk);
        Assert.Equal("planning.plan.mock_exam_questions_must_be_positive", result.Failure.Messages[0].Code);
    }

    [Fact]
    public void Create_WithIncompatiblePerformanceBands_ReturnsInvalid()
    {
        var invalidBandsForStandard = PerformanceBands.Create(-20, 60, ScoringRule.Cebraspe).Data;
        var result = StudyPlan.Create(_ownerId, "Plan", scoringRule: ScoringRule.Standard, performanceBands: invalidBandsForStandard);

        Assert.False(result.IsOk);
        Assert.Equal("planning.bands.low_out_of_range", result.Failure.Messages[0].Code);
    }

    [Fact]
    public void Update_WithValidData_UpdatesAllFields()
    {
        var plan = StudyPlan.Create(_ownerId, "Original Name").Data;
        var newPeriod = PerformancePeriod.LastDays(15).Data;
        var newBands = PerformanceBands.Create(55, 75, ScoringRule.Standard).Data;
        var newDate = new DateOnly(2027, 8, 1);

        var updateResult = plan.Update(
            "New Name",
            "New Description",
            ScoringRule.Standard,
            newPeriod,
            newBands,
            newDate,
            defaultMockExamQuestions: 80,
            fitnessTestEnabled: true);

        Assert.True(updateResult.IsOk);
        Assert.Equal("New Name", plan.Name);
        Assert.Equal("New Description", plan.Description);
        Assert.Equal(ScoringRule.Standard, plan.ScoringRule);
        Assert.Equal(newPeriod, plan.DefaultPerformancePeriod);
        Assert.Equal(newBands, plan.PerformanceBands);
        Assert.Equal(newDate, plan.ExamDate);
        Assert.Equal(80, plan.DefaultMockExamQuestions);
        Assert.True(plan.FitnessTestEnabled);
    }

    [Fact]
    public void Sections_AddRenameAndReorder_OperateCorrectly()
    {
        var plan = StudyPlan.Create(_ownerId, "Plan").Data;

        var sec1Result = plan.AddSection("Conhecimentos Básicos");
        var sec2Result = plan.AddSection("Conhecimentos Específicos");

        Assert.True(sec1Result.IsOk);
        Assert.True(sec2Result.IsOk);
        Assert.Equal(2, plan.Sections.Count);
        Assert.Equal(0, plan.Sections[0].Order);
        Assert.Equal(1, plan.Sections[1].Order);

        var duplicateResult = plan.AddSection("conhecimentos básicos");
        Assert.False(duplicateResult.IsOk);
        Assert.Equal("planning.section.duplicate_name", duplicateResult.Failure.Messages[0].Code);

        var renameResult = plan.RenameSection(sec1Result.Data.Id, "Básicos");
        Assert.True(renameResult.IsOk);
        Assert.Equal("Básicos", plan.Sections.First(s => s.Id == sec1Result.Data.Id).Name);

        var reorderResult = plan.ReorderSections([sec2Result.Data.Id, sec1Result.Data.Id]);
        Assert.True(reorderResult.IsOk);
        Assert.Equal(sec2Result.Data.Id, plan.Sections[0].Id);
        Assert.Equal(sec1Result.Data.Id, plan.Sections[1].Id);
    }

    [Fact]
    public void RemoveSection_DeletesSectionAndClearsSectionIdOnSubjectPlacements()
    {
        var plan = StudyPlan.Create(_ownerId, "Plan").Data;
        var section = plan.AddSection("Gerais").Data;
        var subj1 = SubjectId.New();
        var subj2 = SubjectId.New();

        plan.PlaceSubject(subj1, section.Id);
        plan.PlaceSubject(subj2);

        var removeResult = plan.RemoveSection(section.Id);

        Assert.True(removeResult.IsOk);
        Assert.Empty(plan.Sections);
        var p1 = plan.Outline.First(p => p.SubjectId == subj1);
        var p2 = plan.Outline.First(p => p.SubjectId == subj2);
        Assert.Null(p1.SectionId);
        Assert.Null(p2.SectionId);
    }

    [Fact]
    public void Outline_PlacementMoveAndReorder_OperatesCorrectly()
    {
        var plan = StudyPlan.Create(_ownerId, "Plan").Data;
        var section = plan.AddSection("Bloco 1").Data;

        var s1 = SubjectId.New();
        var s2 = SubjectId.New();
        var s3 = SubjectId.New();

        Assert.True(plan.PlaceSubject(s1, section.Id).IsOk);
        Assert.True(plan.PlaceSubject(s2, section.Id).IsOk);
        Assert.True(plan.PlaceSubject(s3, section.Id).IsOk);

        var duplicateResult = plan.PlaceSubject(s1);
        Assert.False(duplicateResult.IsOk);
        Assert.Equal("planning.outline.duplicate_subject", duplicateResult.Failure.Messages[0].Code);

        var reorderResult = plan.ReorderSubjects(section.Id, [s3, s1, s2]);
        Assert.True(reorderResult.IsOk);
        Assert.Equal(s3, plan.Outline[0].SubjectId);
        Assert.Equal(s1, plan.Outline[1].SubjectId);
        Assert.Equal(s2, plan.Outline[2].SubjectId);

        var moveResult = plan.MoveSubjectToSection(s1, null);
        Assert.True(moveResult.IsOk);
        Assert.Null(plan.Outline.First(p => p.SubjectId == s1).SectionId);

        var removeResult = plan.RemoveSubjectPlacement(s2);
        Assert.True(removeResult.IsOk);
        Assert.Equal(2, plan.Outline.Count);
        Assert.Equal(0, plan.Outline[0].Order);
        Assert.Equal(1, plan.Outline[1].Order);
    }

    [Fact]
    public void ParameterlessConstructors_InitializeDefaults()
    {
        var planCtor = typeof(StudyPlan).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);
        Assert.NotNull(planCtor);
        var plan = (StudyPlan)planCtor.Invoke(null);
        Assert.Equal(string.Empty, plan.Name);
        Assert.Empty(plan.Sections);
        Assert.Empty(plan.Outline);

        var sectionCtor = typeof(Section).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);
        Assert.NotNull(sectionCtor);
        var section = (Section)sectionCtor.Invoke(null);
        Assert.Equal(string.Empty, section.Name);

        var placementCtor = typeof(SubjectPlacement).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);
        Assert.NotNull(placementCtor);
        var placement = (SubjectPlacement)placementCtor.Invoke(null);
        Assert.Null(placement.SectionId);
    }

    [Fact]
    public void Update_WithInvalidArguments_ReturnsExpectedError()
    {
        var plan = StudyPlan.Create(_ownerId, "Original").Data;
        var period = PerformancePeriod.All();
        var bands = PerformanceBands.CreateDefault();

        var emptyNameResult = plan.Update("  ", "Desc", ScoringRule.Cebraspe, period, bands, null, 120, false);
        Assert.False(emptyNameResult.IsOk);
        Assert.Equal("planning.plan.name_required", emptyNameResult.Failure.Messages[0].Code);

        var longName = new string('x', StudyPlan.MaxNameLength + 1);
        var longNameResult = plan.Update(longName, "Desc", ScoringRule.Cebraspe, period, bands, null, 120, false);
        Assert.False(longNameResult.IsOk);
        Assert.Equal("planning.plan.name_too_long", longNameResult.Failure.Messages[0].Code);

        var longDesc = new string('x', StudyPlan.MaxDescriptionLength + 1);
        var longDescResult = plan.Update("Valid", longDesc, ScoringRule.Cebraspe, period, bands, null, 120, false);
        Assert.False(longDescResult.IsOk);
        Assert.Equal("planning.plan.description_too_long", longDescResult.Failure.Messages[0].Code);

        var invalidMockResult = plan.Update("Valid", "Desc", ScoringRule.Cebraspe, period, bands, null, 0, false);
        Assert.False(invalidMockResult.IsOk);
        Assert.Equal("planning.plan.mock_exam_questions_must_be_positive", invalidMockResult.Failure.Messages[0].Code);

        var incompatibleBands = PerformanceBands.Create(-10, 50, ScoringRule.Cebraspe).Data;
        var incompatibleBandsResult = plan.Update("Valid", "Desc", ScoringRule.Standard, period, incompatibleBands, null, 120, false);
        Assert.False(incompatibleBandsResult.IsOk);
        Assert.Equal("planning.bands.low_out_of_range", incompatibleBandsResult.Failure.Messages[0].Code);
    }

    [Fact]
    public void AddSection_WithInvalidName_ReturnsInvalid()
    {
        var plan = StudyPlan.Create(_ownerId, "Plan").Data;

        var emptyResult = plan.AddSection("   ");
        Assert.False(emptyResult.IsOk);
        Assert.Equal("planning.section.name_required", emptyResult.Failure.Messages[0].Code);

        var longName = new string('s', StudyPlan.MaxSectionNameLength + 1);
        var longResult = plan.AddSection(longName);
        Assert.False(longResult.IsOk);
        Assert.Equal("planning.section.name_too_long", longResult.Failure.Messages[0].Code);
    }

    [Fact]
    public void RenameSection_EdgeCases_HandleNotFoundAndValidationErrors()
    {
        var plan = StudyPlan.Create(_ownerId, "Plan").Data;
        var s1 = plan.AddSection("Section 1").Data;
        var s2 = plan.AddSection("Section 2").Data;

        var notFoundResult = plan.RenameSection(SectionId.New(), "New Name");
        Assert.False(notFoundResult.IsOk);
        Assert.Equal("planning.section.not_found", notFoundResult.Failure.Messages[0].Code);

        var emptyNameResult = plan.RenameSection(s1.Id, "   ");
        Assert.False(emptyNameResult.IsOk);
        Assert.Equal("planning.section.name_required", emptyNameResult.Failure.Messages[0].Code);

        var longName = new string('s', StudyPlan.MaxSectionNameLength + 1);
        var longNameResult = plan.RenameSection(s1.Id, longName);
        Assert.False(longNameResult.IsOk);
        Assert.Equal("planning.section.name_too_long", longNameResult.Failure.Messages[0].Code);

        var duplicateResult = plan.RenameSection(s1.Id, "section 2");
        Assert.False(duplicateResult.IsOk);
        Assert.Equal("planning.section.duplicate_name", duplicateResult.Failure.Messages[0].Code);
    }

    [Fact]
    public void ReorderSections_WithInvalidList_ReturnsInvalid()
    {
        var plan = StudyPlan.Create(_ownerId, "Plan").Data;
        var s1 = plan.AddSection("S1").Data;
        var s2 = plan.AddSection("S2").Data;

        var countMismatch = plan.ReorderSections([s1.Id]);
        Assert.False(countMismatch.IsOk);
        Assert.Equal("planning.section.invalid_reorder", countMismatch.Failure.Messages[0].Code);

        var duplicateId = plan.ReorderSections([s1.Id, s1.Id]);
        Assert.False(duplicateId.IsOk);
        Assert.Equal("planning.section.invalid_reorder", duplicateId.Failure.Messages[0].Code);

        var unknownId = plan.ReorderSections([s1.Id, SectionId.New()]);
        Assert.False(unknownId.IsOk);
        Assert.Equal("planning.section.invalid_reorder", unknownId.Failure.Messages[0].Code);
    }

    [Fact]
    public void RemoveSection_EdgeCases_HandleNotFoundAndMultipleRemaining()
    {
        var plan = StudyPlan.Create(_ownerId, "Plan").Data;
        var notFound = plan.RemoveSection(SectionId.New());
        Assert.False(notFound.IsOk);
        Assert.Equal("planning.section.not_found", notFound.Failure.Messages[0].Code);

        var s1 = plan.AddSection("A").Data;
        var s2 = plan.AddSection("B").Data;
        var s3 = plan.AddSection("C").Data;

        var removeB = plan.RemoveSection(s2.Id);
        Assert.True(removeB.IsOk);
        Assert.Equal(2, plan.Sections.Count);
        Assert.Equal(0, plan.Sections[0].Order);
        Assert.Equal(1, plan.Sections[1].Order);
    }

    [Fact]
    public void PlaceSubject_WithMissingSection_ReturnsNotFound()
    {
        var plan = StudyPlan.Create(_ownerId, "Plan").Data;
        var result = plan.PlaceSubject(SubjectId.New(), SectionId.New());

        Assert.False(result.IsOk);
        Assert.Equal("planning.section.not_found", result.Failure.Messages[0].Code);
    }

    [Fact]
    public void MoveSubjectToSection_EdgeCases_HandleNotFoundSubjectAndSection()
    {
        var plan = StudyPlan.Create(_ownerId, "Plan").Data;
        var missingSubject = plan.MoveSubjectToSection(SubjectId.New(), null);
        Assert.False(missingSubject.IsOk);
        Assert.Equal("planning.outline.subject_not_found", missingSubject.Failure.Messages[0].Code);

        var s1 = SubjectId.New();
        plan.PlaceSubject(s1);
        var missingSection = plan.MoveSubjectToSection(s1, SectionId.New());
        Assert.False(missingSection.IsOk);
        Assert.Equal("planning.section.not_found", missingSection.Failure.Messages[0].Code);
    }

    [Fact]
    public void ReorderSubjects_GlobalAndInvalidCases_OperateCorrectly()
    {
        var plan = StudyPlan.Create(_ownerId, "Plan").Data;
        var s1 = SubjectId.New();
        var s2 = SubjectId.New();
        var s3 = SubjectId.New();

        plan.PlaceSubject(s1);
        plan.PlaceSubject(s2);
        plan.PlaceSubject(s3);

        var globalReorder = plan.ReorderSubjects(null, [s3, s1, s2]);
        Assert.True(globalReorder.IsOk);
        Assert.Equal(s3, plan.Outline[0].SubjectId);
        Assert.Equal(s1, plan.Outline[1].SubjectId);
        Assert.Equal(s2, plan.Outline[2].SubjectId);

        var section = plan.AddSection("Sec").Data;
        var invalidScoped = plan.ReorderSubjects(section.Id, [s1]);
        Assert.False(invalidScoped.IsOk);
        Assert.Equal("planning.outline.invalid_reorder", invalidScoped.Failure.Messages[0].Code);
    }

    [Fact]
    public void RemoveSubjectPlacement_WithMissingSubject_ReturnsNotFound()
    {
        var plan = StudyPlan.Create(_ownerId, "Plan").Data;
        var result = plan.RemoveSubjectPlacement(SubjectId.New());

        Assert.False(result.IsOk);
        Assert.Equal("planning.outline.subject_not_found", result.Failure.Messages[0].Code);
    }

    [Fact]
    public void ValidatePerformanceBands_HighThresholdOutOfRangeOrInverted_ReturnsInvalid()
    {
        var plan = StudyPlan.Create(_ownerId, "Plan").Data;

        var bandsCtor = typeof(PerformanceBands).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            [typeof(int), typeof(int)],
            null);
        Assert.NotNull(bandsCtor);

        var highTooHighBands = (PerformanceBands)bandsCtor.Invoke([50, 105]);
        var resultHighTooHigh = plan.Update(
            "Plan",
            "Desc",
            ScoringRule.Standard,
            PerformancePeriod.All(),
            highTooHighBands,
            null,
            120,
            false);

        Assert.False(resultHighTooHigh.IsOk);
        Assert.Equal("planning.bands.high_out_of_range", resultHighTooHigh.Failure.Messages[0].Code);

        var equalBands = (PerformanceBands)bandsCtor.Invoke([70, 70]);
        var resultEqual = plan.Update(
            "Plan",
            "Desc",
            ScoringRule.Standard,
            PerformancePeriod.All(),
            equalBands,
            null,
            120,
            false);

        Assert.False(resultEqual.IsOk);
        Assert.Equal("planning.bands.low_must_be_less_than_high", resultEqual.Failure.Messages[0].Code);
    }
}
