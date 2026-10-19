using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Abstractions;
using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Domain.Planning;
using Trail.Web.Planning;
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
    private readonly IReadOnlyList<IPlanLinkContributor> _linkContributors;

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
        DeleteStudyPlanHandler deleteStudyPlanHandler,
        IEnumerable<IPlanLinkContributor> linkContributors)
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
        ArgumentNullException.ThrowIfNull(linkContributors);

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
        _linkContributors = [.. linkContributors
            .OrderBy(contributor => contributor.Order)
            .ThenBy(contributor => contributor.GetType().FullName, StringComparer.Ordinal)];
    }

    public StudyPlanDetailView Plan { get; private set; } = null!;

    public IReadOnlyList<TopicTreeItemView>? SearchResults { get; private set; }

    public DeletionImpactView? PlanImpact { get; private set; }

    public DeletionConfirmationModel? SubjectDeletion { get; private set; }

    public IReadOnlyList<PlanLink> PlanLinks { get; private set; } = [];

    public Guid? FilterSubjectId { get; set; }

    public TopicStatus? FilterStatus { get; set; }

    public string? SearchQuery { get; set; }

    public AddSectionFormModel AddSectionForm =>
        _submittedForm is { Form: PlanForm.AddSection } submitted
            ? new(Plan.Id.Value, Plan.Version, submitted.Name, ShowErrors: true)
            : new(Plan.Id.Value, Plan.Version, null, ShowErrors: false);

    public AddSubjectFormModel AddSubjectForm =>
        _submittedForm is { Form: PlanForm.AddSubject } submitted
            ? new(Plan.Id.Value, Plan.Version, Plan.Sections, submitted.Name, submitted.SectionId, ShowErrors: true)
            : new(Plan.Id.Value, Plan.Version, Plan.Sections, null, null, ShowErrors: false);

    public SectionNameFormModel SectionNameForm(Guid sectionId, string sectionName) =>
        _submittedForm is { Form: PlanForm.SectionName } submitted && submitted.SectionId == sectionId
            ? new(Plan.Id.Value, Plan.Version, sectionId, submitted.Name ?? string.Empty, ShowErrors: true)
            : new(Plan.Id.Value, Plan.Version, sectionId, sectionName, ShowErrors: false);

    private SubmittedForm? _submittedForm;

    private enum PlanForm
    {
        AddSection,
        AddSubject,
        SectionName,
    }

    private sealed record SubmittedForm(PlanForm Form, string? Name, Guid? SectionId);

    public override async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.HandlerMethod is null)
        {
            await next();
            return;
        }

        var planId = StudyPlanId.From(Guid.Parse(
            Convert.ToString(context.RouteData.Values["planId"], CultureInfo.InvariantCulture)!,
            CultureInfo.InvariantCulture));

        var planResult = await _getStudyPlanHandler.HandleAsync(new GetStudyPlanQuery(planId), context.HttpContext.RequestAborted);
        if (!planResult.IsOk)
        {
            context.Result = this.Respond(planResult, "planning.get_plan");
            return;
        }

        Plan = planResult.Data;

        var executedContext = await next();
        if (executedContext.Result is PageResult)
        {
            var impactResult = await _getDeletionImpactHandler.HandleAsync(
                new GetDeletionImpactQuery(DeletionTargetKind.StudyPlan, Plan.Id),
                context.HttpContext.RequestAborted);

            if (impactResult.IsOk)
            {
                PlanImpact = impactResult.Data;
            }

            PlanLinks = [.. _linkContributors.Select(contributor => contributor.GetLink(Plan)).OfType<PlanLink>()];
        }
    }

    public async Task<IActionResult> OnGetAsync(
        Guid planId,
        Guid? filterSubjectId,
        TopicStatus? filterStatus,
        string? q,
        CancellationToken cancellationToken = default)
    {
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
            if (!searchResult.IsOk)
            {
                return this.Respond(searchResult, "planning.search_topics");
            }

            SearchResults = searchResult.Data;
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

        return this.Respond(searchResult, "planning.search_topics", options =>
        {
            options.PartialViewName = "_SearchResults";
            options.RedirectPage = "/Plans/Details";
            options.RedirectRouteValues = new { planId, filterSubjectId, filterStatus, q };
            options.RedirectGetRequests = true;
        });
    }

    public async Task<IActionResult> OnPostAddSectionAsync(
        Guid planId,
        string? name,
        int expectedPlanVersion,
        CancellationToken cancellationToken = default)
    {
        var command = new AddSectionCommand(StudyPlanId.From(planId), name ?? string.Empty, expectedPlanVersion);
        var result = await _addSectionHandler.HandleAsync(command, cancellationToken);

        _submittedForm = new SubmittedForm(PlanForm.AddSection, name, null);
        return this.Respond(result, "planning.add_section", options =>
        {
            options.FormPartialViewName = "_AddSectionForm";
            options.FormPartialModel = AddSectionForm;
            options.RedirectPage = "/Plans/Details";
            options.RedirectRouteValues = new { planId };
        });
    }

    public async Task<IActionResult> OnPostUpdateSectionAsync(
        Guid planId,
        Guid sectionId,
        string? name,
        int expectedPlanVersion,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateSectionCommand(StudyPlanId.From(planId), SectionId.From(sectionId), name ?? string.Empty, expectedPlanVersion);
        var result = await _updateSectionHandler.HandleAsync(command, cancellationToken);

        _submittedForm = new SubmittedForm(PlanForm.SectionName, name, sectionId);
        return this.Respond(result, "planning.update_section", options =>
        {
            options.FormPartialViewName = "_SectionNameForm";
            options.FormPartialModel = SectionNameForm(sectionId, name ?? string.Empty);
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
        var sections = Plan.Sections.OrderBy(s => s.Order).ToList();
        var index = sections.FindIndex(s => s.Id.Value == sectionId);
        if (index < 0)
        {
            return this.Respond(Result.NotFound(Message.Error("planning.section.not_found")), "planning.reorder_sections");
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
        string? name,
        Guid? sectionId,
        int expectedPlanVersion,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateSubjectCommand(
            StudyPlanId.From(planId),
            name ?? string.Empty,
            sectionId.HasValue ? SectionId.From(sectionId.Value) : null,
            expectedPlanVersion);

        var result = await _createSubjectHandler.HandleAsync(command, cancellationToken);

        _submittedForm = new SubmittedForm(PlanForm.AddSubject, name, sectionId);
        return this.Respond(result, "planning.create_subject", options =>
        {
            options.FormPartialViewName = "_AddSubjectForm";
            options.FormPartialModel = AddSubjectForm;
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
        var targetSectionId = sectionId.HasValue ? SectionId.From(sectionId.Value) : (SectionId?)null;
        var subjects = Plan.Subjects
            .Where(s => s.SectionId == targetSectionId)
            .OrderBy(s => s.Order)
            .ToList();

        var index = subjects.FindIndex(s => s.SubjectId.Value == subjectId);
        if (index < 0)
        {
            return this.Respond(Result.NotFound(Message.Error("planning.subject.not_found")), "planning.reorder_subjects");
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

    public async Task<IActionResult> OnGetDeleteSubjectImpactAsync(
        Guid planId,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetDeletionImpactQuery(DeletionTargetKind.Subject, Plan.Id, SubjectId: SubjectId.From(subjectId));
        var result = await _getDeletionImpactHandler.HandleAsync(query, cancellationToken);
        if (result.IsOk)
        {
            SubjectDeletion = DeletionConfirmationModel.ForSubject(result.Data, planId, DeletionConfirmationModel.SubjectSectionDescriptionId);
        }

        return this.Respond(result, "planning.get_subject_deletion_impact", options =>
        {
            options.PartialViewName = DeletionConfirmationModel.PartialViewName;
            options.PartialModelFactory = impact => DeletionConfirmationModel.ForSubject(impact, planId);
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
