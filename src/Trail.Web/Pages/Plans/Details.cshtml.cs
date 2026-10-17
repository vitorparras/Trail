using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Planning;
using Trail.Domain.Planning;
using Trail.Web.Htmx;
using Trail.Web.Results;

namespace Trail.Web.Pages.Plans;

public sealed class DetailsModel : PageModel
{
    private readonly GetStudyPlanHandler _getStudyPlanHandler;
    private readonly SearchPlanTopicsHandler _searchPlanTopicsHandler;
    private readonly AddSectionHandler _addSectionHandler;
    private readonly UpdateSectionHandler _updateSectionHandler;
    private readonly ReorderSectionsHandler _reorderSectionsHandler;
    private readonly DeleteSectionHandler _deleteSectionHandler;
    private readonly CreateSubjectHandler _createSubjectHandler;
    private readonly MoveSubjectHandler _moveSubjectHandler;
    private readonly ReorderSubjectsHandler _reorderSubjectsHandler;
    private readonly DeleteSubjectHandler _deleteSubjectHandler;
    private readonly GetDeletionImpactHandler _getDeletionImpactHandler;
    private readonly DeleteStudyPlanHandler _deleteStudyPlanHandler;

    public DetailsModel(
        GetStudyPlanHandler getStudyPlanHandler,
        SearchPlanTopicsHandler searchPlanTopicsHandler,
        AddSectionHandler addSectionHandler,
        UpdateSectionHandler updateSectionHandler,
        ReorderSectionsHandler reorderSectionsHandler,
        DeleteSectionHandler deleteSectionHandler,
        CreateSubjectHandler createSubjectHandler,
        MoveSubjectHandler moveSubjectHandler,
        ReorderSubjectsHandler reorderSubjectsHandler,
        DeleteSubjectHandler deleteSubjectHandler,
        GetDeletionImpactHandler getDeletionImpactHandler,
        DeleteStudyPlanHandler deleteStudyPlanHandler)
    {
        ArgumentNullException.ThrowIfNull(getStudyPlanHandler);
        ArgumentNullException.ThrowIfNull(searchPlanTopicsHandler);
        ArgumentNullException.ThrowIfNull(addSectionHandler);
        ArgumentNullException.ThrowIfNull(updateSectionHandler);
        ArgumentNullException.ThrowIfNull(reorderSectionsHandler);
        ArgumentNullException.ThrowIfNull(deleteSectionHandler);
        ArgumentNullException.ThrowIfNull(createSubjectHandler);
        ArgumentNullException.ThrowIfNull(moveSubjectHandler);
        ArgumentNullException.ThrowIfNull(reorderSubjectsHandler);
        ArgumentNullException.ThrowIfNull(deleteSubjectHandler);
        ArgumentNullException.ThrowIfNull(getDeletionImpactHandler);
        ArgumentNullException.ThrowIfNull(deleteStudyPlanHandler);

        _getStudyPlanHandler = getStudyPlanHandler;
        _searchPlanTopicsHandler = searchPlanTopicsHandler;
        _addSectionHandler = addSectionHandler;
        _updateSectionHandler = updateSectionHandler;
        _reorderSectionsHandler = reorderSectionsHandler;
        _deleteSectionHandler = deleteSectionHandler;
        _createSubjectHandler = createSubjectHandler;
        _moveSubjectHandler = moveSubjectHandler;
        _reorderSubjectsHandler = reorderSubjectsHandler;
        _deleteSubjectHandler = deleteSubjectHandler;
        _getDeletionImpactHandler = getDeletionImpactHandler;
        _deleteStudyPlanHandler = deleteStudyPlanHandler;
    }

    public StudyPlanDetailView Plan { get; private set; } = null!;

    public IReadOnlyList<TopicTreeItemView>? SearchResults { get; private set; }

    public DeletionImpactView? PlanImpact { get; private set; }

    public Guid? FilterSubjectId { get; set; }

    public TopicStatus? FilterStatus { get; set; }

    public string? SearchQuery { get; set; }

    public async Task<IActionResult> OnGetAsync(
        Guid planId,
        Guid? filterSubjectId,
        TopicStatus? filterStatus,
        string? q,
        CancellationToken cancellationToken = default)
    {
        var planResult = await _getStudyPlanHandler.HandleAsync(new GetStudyPlanQuery(StudyPlanId.From(planId)), cancellationToken);
        if (!planResult.IsOk)
        {
            return this.Respond(planResult, "planning.get_plan");
        }

        Plan = planResult.Data;
        FilterSubjectId = filterSubjectId;
        FilterStatus = filterStatus;
        SearchQuery = q;

        if (filterSubjectId.HasValue || filterStatus.HasValue || !string.IsNullOrWhiteSpace(q))
        {
            var searchQuery = new SearchPlanTopicsQuery(
                Plan.Id,
                filterSubjectId.HasValue ? SubjectId.From(filterSubjectId.Value) : null,
                filterStatus,
                q);

            var searchResult = await _searchPlanTopicsHandler.HandleAsync(searchQuery, cancellationToken);
            if (searchResult.IsOk)
            {
                SearchResults = searchResult.Data;
            }
        }

        var impactResult = await _getDeletionImpactHandler.HandleAsync(
            new GetDeletionImpactQuery(DeletionTargetKind.StudyPlan, Plan.Id),
            cancellationToken);

        if (impactResult.IsOk)
        {
            PlanImpact = impactResult.Data;
        }

        return Page();
    }

    public async Task<IActionResult> OnGetSearchTopicsAsync(
        Guid planId,
        Guid? filterSubjectId,
        TopicStatus? filterStatus,
        string? q,
        CancellationToken cancellationToken = default)
    {
        var searchQuery = new SearchPlanTopicsQuery(
            StudyPlanId.From(planId),
            filterSubjectId.HasValue ? SubjectId.From(filterSubjectId.Value) : null,
            filterStatus,
            q);

        var searchResult = await _searchPlanTopicsHandler.HandleAsync(searchQuery, cancellationToken);
        if (!searchResult.IsOk)
        {
            return this.Respond(searchResult, "planning.search_topics");
        }

        if (Request.IsHtmxPartialRequest())
        {
            ViewData["PlanId"] = planId.ToString();
            return Partial("_SearchResults", searchResult.Data);
        }

        return RedirectToPage("/Plans/Details", new
        {
            planId,
            filterSubjectId,
            filterStatus,
            q
        });
    }

    public async Task<IActionResult> OnPostAddSectionAsync(
        Guid planId,
        string name,
        int expectedPlanVersion,
        CancellationToken cancellationToken = default)
    {
        var command = new AddSectionCommand(StudyPlanId.From(planId), name, expectedPlanVersion);
        var result = await _addSectionHandler.HandleAsync(command, cancellationToken);

        return this.Respond(result, "planning.add_section", options =>
        {
            options.RedirectPage = "/Plans/Details";
            options.RedirectRouteValues = new { planId };
        });
    }

    public async Task<IActionResult> OnPostUpdateSectionAsync(
        Guid planId,
        Guid sectionId,
        string name,
        int expectedPlanVersion,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateSectionCommand(StudyPlanId.From(planId), SectionId.From(sectionId), name, expectedPlanVersion);
        var result = await _updateSectionHandler.HandleAsync(command, cancellationToken);

        return this.Respond(result, "planning.update_section", options =>
        {
            options.RedirectPage = "/Plans/Details";
            options.RedirectRouteValues = new { planId };
        });
    }

    public async Task<IActionResult> OnPostReorderSectionsAsync(
        Guid planId,
        Guid sectionId,
        string direction,
        int expectedPlanVersion,
        CancellationToken cancellationToken = default)
    {
        var planResult = await _getStudyPlanHandler.HandleAsync(new GetStudyPlanQuery(StudyPlanId.From(planId)), cancellationToken);
        if (!planResult.IsOk)
        {
            return this.Respond(planResult, "planning.get_plan");
        }

        var sections = planResult.Data.Sections.OrderBy(s => s.Order).ToList();
        var index = sections.FindIndex(s => s.Id.Value == sectionId);
        if (index < 0)
        {
            return RedirectToPage("/Plans/Details", new { planId });
        }

        var newIndex = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase) ? index - 1 : index + 1;
        if (newIndex < 0 || newIndex >= sections.Count)
        {
            return RedirectToPage("/Plans/Details", new { planId });
        }

        var item = sections[index];
        sections.RemoveAt(index);
        sections.Insert(newIndex, item);

        var orderedIds = sections.Select(s => s.Id).ToList();
        var result = await _reorderSectionsHandler.HandleAsync(
            new ReorderSectionsCommand(StudyPlanId.From(planId), orderedIds, expectedPlanVersion),
            cancellationToken);

        return this.Respond(result, "planning.reorder_sections", options =>
        {
            options.RedirectPage = "/Plans/Details";
            options.RedirectRouteValues = new { planId };
        });
    }

    public async Task<IActionResult> OnPostDeleteSectionAsync(
        Guid planId,
        Guid sectionId,
        int expectedPlanVersion,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteSectionCommand(StudyPlanId.From(planId), SectionId.From(sectionId), expectedPlanVersion);
        var result = await _deleteSectionHandler.HandleAsync(command, cancellationToken);

        return this.Respond(result, "planning.delete_section", options =>
        {
            options.RedirectPage = "/Plans/Details";
            options.RedirectRouteValues = new { planId };
        });
    }

    public async Task<IActionResult> OnPostAddSubjectAsync(
        Guid planId,
        string name,
        Guid? sectionId,
        int expectedPlanVersion,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateSubjectCommand(
            StudyPlanId.From(planId),
            name,
            sectionId.HasValue ? SectionId.From(sectionId.Value) : null,
            expectedPlanVersion);

        var result = await _createSubjectHandler.HandleAsync(command, cancellationToken);

        return this.Respond(result, "planning.create_subject", options =>
        {
            options.RedirectPage = "/Plans/Details";
            options.RedirectRouteValues = new { planId };
        });
    }

    public async Task<IActionResult> OnPostMoveSubjectAsync(
        Guid planId,
        Guid subjectId,
        Guid? newSectionId,
        int expectedPlanVersion,
        CancellationToken cancellationToken = default)
    {
        var command = new MoveSubjectCommand(
            StudyPlanId.From(planId),
            SubjectId.From(subjectId),
            newSectionId.HasValue ? SectionId.From(newSectionId.Value) : null,
            expectedPlanVersion);

        var result = await _moveSubjectHandler.HandleAsync(command, cancellationToken);

        return this.Respond(result, "planning.move_subject", options =>
        {
            options.RedirectPage = "/Plans/Details";
            options.RedirectRouteValues = new { planId };
        });
    }

    public async Task<IActionResult> OnPostReorderSubjectsAsync(
        Guid planId,
        Guid? sectionId,
        Guid subjectId,
        string direction,
        int expectedPlanVersion,
        CancellationToken cancellationToken = default)
    {
        var planResult = await _getStudyPlanHandler.HandleAsync(new GetStudyPlanQuery(StudyPlanId.From(planId)), cancellationToken);
        if (!planResult.IsOk)
        {
            return this.Respond(planResult, "planning.get_plan");
        }

        var targetSectionId = sectionId.HasValue ? SectionId.From(sectionId.Value) : (SectionId?)null;
        var subjects = planResult.Data.Subjects
            .Where(s => s.SectionId == targetSectionId)
            .OrderBy(s => s.Order)
            .ToList();

        var index = subjects.FindIndex(s => s.SubjectId.Value == subjectId);
        if (index < 0)
        {
            return RedirectToPage("/Plans/Details", new { planId });
        }

        var newIndex = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase) ? index - 1 : index + 1;
        if (newIndex < 0 || newIndex >= subjects.Count)
        {
            return RedirectToPage("/Plans/Details", new { planId });
        }

        var item = subjects[index];
        subjects.RemoveAt(index);
        subjects.Insert(newIndex, item);

        var orderedIds = subjects.Select(s => s.SubjectId).ToList();
        var result = await _reorderSubjectsHandler.HandleAsync(
            new ReorderSubjectsCommand(StudyPlanId.From(planId), targetSectionId, orderedIds, expectedPlanVersion),
            cancellationToken);

        return this.Respond(result, "planning.reorder_subjects", options =>
        {
            options.RedirectPage = "/Plans/Details";
            options.RedirectRouteValues = new { planId };
        });
    }

    public async Task<IActionResult> OnPostDeleteSubjectAsync(
        Guid planId,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteSubjectCommand(StudyPlanId.From(planId), SubjectId.From(subjectId));
        var result = await _deleteSubjectHandler.HandleAsync(command, cancellationToken);

        return this.Respond(result, "planning.delete_subject", options =>
        {
            options.RedirectPage = "/Plans/Details";
            options.RedirectRouteValues = new { planId };
        });
    }

    public async Task<IActionResult> OnPostDeletePlanAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteStudyPlanCommand(StudyPlanId.From(planId));
        var result = await _deleteStudyPlanHandler.HandleAsync(command, cancellationToken);

        return this.Respond(result, "planning.delete_plan", options =>
        {
            options.RedirectPage = "/Plans/Index";
        });
    }
}
