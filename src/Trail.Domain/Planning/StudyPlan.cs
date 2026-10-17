
using Trail.Domain.Common;

namespace Trail.Domain.Planning;

public sealed class StudyPlan : AggregateRoot<StudyPlanId>
{
    public const int DefaultMockExamQuestionsCount = 120;
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxSectionNameLength = 200;

    private readonly List<Section> _sections = [];
    private readonly List<SubjectPlacement> _outline = [];

    private StudyPlan()
    {
    }

    private StudyPlan(
        StudyPlanId id,
        UserId ownerId,
        string name,
        string description,
        ScoringRule scoringRule,
        PerformancePeriod defaultPerformancePeriod,
        PerformanceBands performanceBands,
        DateOnly? examDate,
        int defaultMockExamQuestions,
        bool fitnessTestEnabled)
        : base(id, ownerId)
    {
        Name = name;
        Description = description;
        ScoringRule = scoringRule;
        DefaultPerformancePeriod = defaultPerformancePeriod;
        PerformanceBands = performanceBands;
        ExamDate = examDate;
        DefaultMockExamQuestions = defaultMockExamQuestions;
        FitnessTestEnabled = fitnessTestEnabled;
    }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public ScoringRule ScoringRule { get; private set; }

    public PerformancePeriod DefaultPerformancePeriod { get; private set; } = PerformancePeriod.All();

    public PerformanceBands PerformanceBands { get; private set; } = PerformanceBands.CreateDefault();

    public DateOnly? ExamDate { get; private set; }

    public int DefaultMockExamQuestions { get; private set; } = DefaultMockExamQuestionsCount;

    public bool FitnessTestEnabled { get; private set; }

    public IReadOnlyList<Section> Sections => _sections.OrderBy(s => s.Order).ToList();

    public IReadOnlyList<SubjectPlacement> Outline => _outline.OrderBy(o => o.Order).ToList();

    public static Result<StudyPlan> Create(
        UserId ownerId,
        string? name,
        string? description = null,
        ScoringRule scoringRule = ScoringRule.Cebraspe,
        PerformancePeriod? defaultPerformancePeriod = null,
        PerformanceBands? performanceBands = null,
        DateOnly? examDate = null,
        int defaultMockExamQuestions = DefaultMockExamQuestionsCount,
        bool fitnessTestEnabled = false,
        StudyPlanOrigin origin = StudyPlanOrigin.Manual)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Invalid(Message.Error("planning.plan.name_required", nameof(Name)));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > MaxNameLength)
        {
            return Result.Invalid(Message.Error("planning.plan.name_too_long", nameof(Name)));
        }

        var trimmedDescription = description?.Trim() ?? string.Empty;
        if (trimmedDescription.Length > MaxDescriptionLength)
        {
            return Result.Invalid(Message.Error("planning.plan.description_too_long", nameof(Description)));
        }

        if (defaultMockExamQuestions <= 0)
        {
            return Result.Invalid(Message.Error("planning.plan.mock_exam_questions_must_be_positive", nameof(DefaultMockExamQuestions)));
        }

        var bands = performanceBands ?? PerformanceBands.CreateDefault();
        var bandsValidation = ValidatePerformanceBandsForScoringRule(bands, scoringRule);
        if (!bandsValidation.IsOk)
        {
            return bandsValidation.Failure;
        }

        var period = defaultPerformancePeriod ?? PerformancePeriod.All();

        var plan = new StudyPlan(
            StudyPlanId.New(),
            ownerId,
            trimmedName,
            trimmedDescription,
            scoringRule,
            period,
            bands,
            examDate,
            defaultMockExamQuestions,
            fitnessTestEnabled);

        plan.Raise(new StudyPlanCreated(plan.Id, ownerId, origin));

        return Result.Ok(plan);
    }

    public Result Update(
        string? name,
        string? description,
        ScoringRule scoringRule,
        PerformancePeriod defaultPerformancePeriod,
        PerformanceBands performanceBands,
        DateOnly? examDate,
        int defaultMockExamQuestions,
        bool fitnessTestEnabled)
    {
        ArgumentNullException.ThrowIfNull(defaultPerformancePeriod);
        ArgumentNullException.ThrowIfNull(performanceBands);

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Invalid(Message.Error("planning.plan.name_required", nameof(Name)));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > MaxNameLength)
        {
            return Result.Invalid(Message.Error("planning.plan.name_too_long", nameof(Name)));
        }

        var trimmedDescription = description?.Trim() ?? string.Empty;
        if (trimmedDescription.Length > MaxDescriptionLength)
        {
            return Result.Invalid(Message.Error("planning.plan.description_too_long", nameof(Description)));
        }

        if (defaultMockExamQuestions <= 0)
        {
            return Result.Invalid(Message.Error("planning.plan.mock_exam_questions_must_be_positive", nameof(DefaultMockExamQuestions)));
        }

        var bandsValidation = ValidatePerformanceBandsForScoringRule(performanceBands, scoringRule);
        if (!bandsValidation.IsOk)
        {
            return bandsValidation.Failure;
        }

        Name = trimmedName;
        Description = trimmedDescription;
        ScoringRule = scoringRule;
        DefaultPerformancePeriod = defaultPerformancePeriod;
        PerformanceBands = performanceBands;
        ExamDate = examDate;
        DefaultMockExamQuestions = defaultMockExamQuestions;
        FitnessTestEnabled = fitnessTestEnabled;

        return Result.Ok();
    }

    public Result<Section> AddSection(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Invalid(Message.Error("planning.section.name_required", nameof(Section.Name)));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > MaxSectionNameLength)
        {
            return Result.Invalid(Message.Error("planning.section.name_too_long", nameof(Section.Name)));
        }

        if (_sections.Any(s => string.Equals(s.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Invalid(Message.Error("planning.section.duplicate_name", nameof(Section.Name)));
        }

        var order = _sections.Count == 0 ? 0 : _sections.Max(s => s.Order) + 1;
        var section = new Section(SectionId.New(), Id, trimmedName, order);
        _sections.Add(section);

        return Result.Ok(section);
    }

    public Result RenameSection(SectionId sectionId, string? name)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId);
        if (section is null)
        {
            return Result.NotFound(Message.Error("planning.section.not_found", nameof(SectionId)));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Invalid(Message.Error("planning.section.name_required", nameof(Section.Name)));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > MaxSectionNameLength)
        {
            return Result.Invalid(Message.Error("planning.section.name_too_long", nameof(Section.Name)));
        }

        if (_sections.Any(s => s.Id != sectionId && string.Equals(s.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Invalid(Message.Error("planning.section.duplicate_name", nameof(Section.Name)));
        }

        section.Rename(trimmedName);
        return Result.Ok();
    }

    public Result ReorderSections(IReadOnlyList<SectionId> orderedSectionIds)
    {
        ArgumentNullException.ThrowIfNull(orderedSectionIds);

        if (orderedSectionIds.Count != _sections.Count
            || orderedSectionIds.Distinct().Count() != _sections.Count
            || !orderedSectionIds.All(id => _sections.Any(s => s.Id == id)))
        {
            return Result.Invalid(Message.Error("planning.section.invalid_reorder", nameof(orderedSectionIds)));
        }

        for (var i = 0; i < orderedSectionIds.Count; i++)
        {
            var section = _sections.First(s => s.Id == orderedSectionIds[i]);
            section.SetOrder(i);
        }

        return Result.Ok();
    }

    public Result RemoveSection(SectionId sectionId)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId);
        if (section is null)
        {
            return Result.NotFound(Message.Error("planning.section.not_found", nameof(SectionId)));
        }

        _sections.Remove(section);

        var sorted = _sections.OrderBy(s => s.Order).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].SetOrder(i);
        }

        foreach (var placement in _outline.Where(p => p.SectionId == sectionId))
        {
            placement.SetSection(null);
        }

        return Result.Ok();
    }

    public Result PlaceSubject(SubjectId subjectId, SectionId? sectionId = null)
    {
        if (sectionId.HasValue && !_sections.Any(s => s.Id == sectionId.Value))
        {
            return Result.NotFound(Message.Error("planning.section.not_found", nameof(SectionId)));
        }

        if (_outline.Any(p => p.SubjectId == subjectId))
        {
            return Result.Conflict(Message.Error("planning.outline.duplicate_subject", nameof(SubjectId)));
        }

        var order = _outline.Count == 0 ? 0 : _outline.Max(p => p.Order) + 1;
        _outline.Add(new SubjectPlacement(Id, subjectId, sectionId, order));

        return Result.Ok();
    }

    public Result MoveSubjectToSection(SubjectId subjectId, SectionId? sectionId)
    {
        var placement = _outline.FirstOrDefault(p => p.SubjectId == subjectId);
        if (placement is null)
        {
            return Result.NotFound(Message.Error("planning.outline.subject_not_found", nameof(SubjectId)));
        }

        if (sectionId.HasValue && !_sections.Any(s => s.Id == sectionId.Value))
        {
            return Result.NotFound(Message.Error("planning.section.not_found", nameof(SectionId)));
        }

        placement.SetSection(sectionId);
        return Result.Ok();
    }

    public Result ReorderSubjects(SectionId? sectionId, IReadOnlyList<SubjectId> orderedSubjectIds)
    {
        ArgumentNullException.ThrowIfNull(orderedSubjectIds);

        if (sectionId is null
            && orderedSubjectIds.Count == _outline.Count
            && orderedSubjectIds.Distinct().Count() == _outline.Count
            && orderedSubjectIds.All(id => _outline.Any(p => p.SubjectId == id)))
        {
            for (var i = 0; i < orderedSubjectIds.Count; i++)
            {
                var placement = _outline.First(p => p.SubjectId == orderedSubjectIds[i]);
                placement.SetOrder(i);
            }

            return Result.Ok();
        }

        var scoped = _outline.Where(p => p.SectionId == sectionId).ToList();
        if (orderedSubjectIds.Count != scoped.Count
            || orderedSubjectIds.Distinct().Count() != scoped.Count
            || !orderedSubjectIds.All(id => scoped.Any(p => p.SubjectId == id)))
        {
            return Result.Invalid(Message.Error("planning.outline.invalid_reorder", nameof(orderedSubjectIds)));
        }

        var slotOrders = scoped.OrderBy(p => p.Order).Select(p => p.Order).ToList();
        for (var i = 0; i < orderedSubjectIds.Count; i++)
        {
            var placement = scoped.First(p => p.SubjectId == orderedSubjectIds[i]);
            placement.SetOrder(slotOrders[i]);
        }

        return Result.Ok();
    }

    public Result RemoveSubjectPlacement(SubjectId subjectId)
    {
        var placement = _outline.FirstOrDefault(p => p.SubjectId == subjectId);
        if (placement is null)
        {
            return Result.NotFound(Message.Error("planning.outline.subject_not_found", nameof(SubjectId)));
        }

        _outline.Remove(placement);

        var sorted = _outline.OrderBy(p => p.Order).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].SetOrder(i);
        }

        return Result.Ok();
    }

    private static Result ValidatePerformanceBandsForScoringRule(PerformanceBands bands, ScoringRule scoringRule)
    {
        var minAllowed = scoringRule == ScoringRule.Cebraspe ? -100 : 0;
        if (bands.LowThreshold < minAllowed || bands.LowThreshold > 100)
        {
            return Result.Invalid(Message.Error("planning.bands.low_out_of_range", nameof(PerformanceBands.LowThreshold)));
        }

        if (bands.HighThreshold < minAllowed || bands.HighThreshold > 100)
        {
            return Result.Invalid(Message.Error("planning.bands.high_out_of_range", nameof(PerformanceBands.HighThreshold)));
        }

        if (bands.LowThreshold >= bands.HighThreshold)
        {
            return Result.Invalid(Message.Error("planning.bands.low_must_be_less_than_high", nameof(PerformanceBands.LowThreshold)));
        }

        return Result.Ok();
    }
}
