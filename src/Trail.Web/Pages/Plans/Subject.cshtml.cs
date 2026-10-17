using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Planning;
using Trail.Domain.Common;
using Trail.Domain.Planning;
using Trail.Web.Htmx;
using Trail.Web.Markdown;
using Trail.Web.Results;

namespace Trail.Web.Pages.Plans;

public sealed class SubjectModel : PageModel
{
    private readonly GetSubjectTopicsHandler _getSubjectTopicsHandler;
    private readonly UpdateSubjectHandler _updateSubjectHandler;
    private readonly AddTopicHandler _addTopicHandler;
    private readonly ChangeTopicStatusHandler _changeTopicStatusHandler;
    private readonly UpdateTopicHandler _updateTopicHandler;
    private readonly MoveTopicHandler _moveTopicHandler;
    private readonly ReorderTopicsHandler _reorderTopicsHandler;
    private readonly DeleteTopicHandler _deleteTopicHandler;
    private readonly GetTopicNoteHandler _getTopicNoteHandler;
    private readonly SaveTopicNoteHandler _saveTopicNoteHandler;
    private readonly GetDeletionImpactHandler _getDeletionImpactHandler;
    private readonly IMarkdownRenderer _markdownRenderer;

    public SubjectModel(
        GetSubjectTopicsHandler getSubjectTopicsHandler,
        UpdateSubjectHandler updateSubjectHandler,
        AddTopicHandler addTopicHandler,
        ChangeTopicStatusHandler changeTopicStatusHandler,
        UpdateTopicHandler updateTopicHandler,
        MoveTopicHandler moveTopicHandler,
        ReorderTopicsHandler reorderTopicsHandler,
        DeleteTopicHandler deleteTopicHandler,
        GetTopicNoteHandler getTopicNoteHandler,
        SaveTopicNoteHandler saveTopicNoteHandler,
        GetDeletionImpactHandler getDeletionImpactHandler,
        IMarkdownRenderer markdownRenderer)
    {
        ArgumentNullException.ThrowIfNull(getSubjectTopicsHandler);
        ArgumentNullException.ThrowIfNull(updateSubjectHandler);
        ArgumentNullException.ThrowIfNull(addTopicHandler);
        ArgumentNullException.ThrowIfNull(changeTopicStatusHandler);
        ArgumentNullException.ThrowIfNull(updateTopicHandler);
        ArgumentNullException.ThrowIfNull(moveTopicHandler);
        ArgumentNullException.ThrowIfNull(reorderTopicsHandler);
        ArgumentNullException.ThrowIfNull(deleteTopicHandler);
        ArgumentNullException.ThrowIfNull(getTopicNoteHandler);
        ArgumentNullException.ThrowIfNull(saveTopicNoteHandler);
        ArgumentNullException.ThrowIfNull(getDeletionImpactHandler);
        ArgumentNullException.ThrowIfNull(markdownRenderer);

        _getSubjectTopicsHandler = getSubjectTopicsHandler;
        _updateSubjectHandler = updateSubjectHandler;
        _addTopicHandler = addTopicHandler;
        _changeTopicStatusHandler = changeTopicStatusHandler;
        _updateTopicHandler = updateTopicHandler;
        _moveTopicHandler = moveTopicHandler;
        _reorderTopicsHandler = reorderTopicsHandler;
        _deleteTopicHandler = deleteTopicHandler;
        _getTopicNoteHandler = getTopicNoteHandler;
        _saveTopicNoteHandler = saveTopicNoteHandler;
        _getDeletionImpactHandler = getDeletionImpactHandler;
        _markdownRenderer = markdownRenderer;
    }

    public SubjectDetailView Subject { get; private set; } = null!;

    public TopicNoteView? SelectedNote { get; private set; }

    public string RenderedMarkdownHtml { get; private set; } = string.Empty;

    public string NoteMarkdown { get; set; } = string.Empty;

    public int NoteExpectedVersion { get; set; }

    public bool HasNoteConflict { get; private set; }

    public bool HasSubjectConflict { get; private set; }

    public bool HasTopicConflict { get; private set; }

    public string? SubjectNameInput { get; set; }

    public int SubjectExpectedVersion { get; set; }

    public Guid? ConflictTopicId { get; set; }

    public string? ConflictTopicTitle { get; set; }

    public string? ConflictTopicCode { get; set; }

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

        var planId = StudyPlanId.From(ReadRouteGuid(context, "planId"));
        var subjectId = SubjectId.From(ReadRouteGuid(context, "subjectId"));

        var subjectResult = await _getSubjectTopicsHandler.HandleAsync(
            new GetSubjectTopicsQuery(subjectId),
            context.HttpContext.RequestAborted);

        if (!subjectResult.IsOk)
        {
            context.Result = this.Respond(subjectResult, "planning.get_subject");
            return;
        }

        if (subjectResult.Data.StudyPlanId != planId)
        {
            context.Result = this.Respond(Result.NotFound(Message.Error("planning.subject.not_found")), "planning.get_subject");
            return;
        }

        ShowSubject(subjectResult.Data);
        await next();
    }

    public async Task<IActionResult> OnGetAsync(
        Guid planId,
        Guid subjectId,
        Guid? noteTopicId,
        CancellationToken cancellationToken = default)
    {
        var targetTopicId = noteTopicId ?? (Subject.Topics.Count > 0 ? Subject.Topics[0].Id.Value : null);
        if (!targetTopicId.HasValue)
        {
            return Page();
        }

        var noteResult = await _getTopicNoteHandler.HandleAsync(
            new GetTopicNoteQuery(Subject.Id, TopicId.From(targetTopicId.Value)),
            cancellationToken);

        if (!noteResult.IsOk)
        {
            return this.Respond(noteResult, "planning.get_topic_note");
        }

        ShowNote(noteResult.Data);
        return Page();
    }

    public async Task<IActionResult> OnGetTopicNoteAsync(
        Guid planId,
        Guid subjectId,
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        var noteResult = await _getTopicNoteHandler.HandleAsync(
            new GetTopicNoteQuery(Subject.Id, TopicId.From(topicId)),
            cancellationToken);

        if (noteResult.IsOk)
        {
            ShowNote(noteResult.Data);
        }

        return this.Respond(noteResult, "planning.get_topic_note", options =>
        {
            options.PartialViewName = "_TopicNote";
            options.PartialModelFactory = _ => this;
        });
    }

    public async Task<IActionResult> OnPostSaveNoteAsync(
        Guid planId,
        Guid subjectId,
        Guid topicId,
        string? markdown,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var command = new SaveTopicNoteCommand(
            SubjectId.From(subjectId),
            TopicId.From(topicId),
            markdown,
            expectedVersion);

        var result = await _saveTopicNoteHandler.HandleAsync(command, cancellationToken);
        if (result.Status == ResultStatus.Conflict)
        {
            var latestNoteResult = await _getTopicNoteHandler.HandleAsync(
                new GetTopicNoteQuery(SubjectId.From(subjectId), TopicId.From(topicId)),
                cancellationToken);

            HasNoteConflict = true;
            NoteMarkdown = markdown ?? string.Empty;
            NoteExpectedVersion = latestNoteResult.IsOk ? latestNoteResult.Data.Version : expectedVersion;
            RenderedMarkdownHtml = _markdownRenderer.RenderToHtml(markdown);

            if (latestNoteResult.IsOk)
            {
                SelectedNote = new TopicNoteView(
                    TopicId.From(topicId),
                    SubjectId.From(subjectId),
                    StudyPlanId.From(planId),
                    latestNoteResult.Data.TopicTitle,
                    latestNoteResult.Data.TopicCode,
                    latestNoteResult.Data.NoteId,
                    markdown ?? string.Empty,
                    NoteExpectedVersion);
            }

            Response.StatusCode = StatusCodes.Status409Conflict;
            return Page();
        }

        if (!result.IsOk)
        {
            return this.Respond(result, "planning.save_topic_note");
        }

        return RedirectToPage("/Plans/Subject", new
        {
            planId,
            subjectId,
            noteTopicId = topicId
        });
    }

    public async Task<IActionResult> OnPostUpdateSubjectAsync(
        Guid planId,
        Guid subjectId,
        string name,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateSubjectCommand(SubjectId.From(subjectId), name, expectedVersion);
        var result = await _updateSubjectHandler.HandleAsync(command, cancellationToken);

        if (result.Status == ResultStatus.Conflict)
        {
            await RefreshSubjectAsync(subjectId, cancellationToken);
            HasSubjectConflict = true;
            SubjectNameInput = name;
            Response.StatusCode = StatusCodes.Status409Conflict;
            return Page();
        }

        if (!result.IsOk)
        {
            return this.Respond(result, "planning.update_subject");
        }

        return RedirectToPage("/Plans/Subject", new { planId, subjectId });
    }

    public async Task<IActionResult> OnPostAddTopicAsync(
        Guid planId,
        Guid subjectId,
        string? code,
        string title,
        Guid? parentId,
        TopicStatus initialStatus,
        int? expectedSubjectVersion,
        CancellationToken cancellationToken = default)
    {
        var command = new AddTopicCommand(
            SubjectId.From(subjectId),
            title,
            code,
            parentId.HasValue ? TopicId.From(parentId.Value) : null,
            initialStatus,
            expectedSubjectVersion);

        var result = await _addTopicHandler.HandleAsync(command, cancellationToken);

        return this.Respond(result, "planning.add_topic", options =>
        {
            options.RedirectPage = "/Plans/Subject";
            options.RedirectRouteValues = new { planId, subjectId };
        });
    }

    public async Task<IActionResult> OnPostChangeStatusAsync(
        Guid planId,
        Guid subjectId,
        Guid topicId,
        TopicStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var command = new ChangeTopicStatusCommand(SubjectId.From(subjectId), TopicId.From(topicId), newStatus);
        var result = await _changeTopicStatusHandler.HandleAsync(command, cancellationToken);

        var updatedSubject = Subject;
        if (result.IsOk && Request.IsHtmxPartialRequest())
        {
            var subjectResult = await _getSubjectTopicsHandler.HandleAsync(new GetSubjectTopicsQuery(SubjectId.From(subjectId)), cancellationToken);
            if (!subjectResult.IsOk)
            {
                return this.Respond(subjectResult, "planning.get_subject");
            }

            updatedSubject = subjectResult.Data;
        }

        return this.Respond(result, "planning.change_topic_status", options =>
        {
            options.PartialViewName = "_TopicTree";
            options.PartialModel = updatedSubject;
            options.RedirectPage = "/Plans/Subject";
            options.RedirectRouteValues = new { planId, subjectId };
        });
    }

    public async Task<IActionResult> OnPostUpdateTopicAsync(
        Guid planId,
        Guid subjectId,
        Guid topicId,
        string? code,
        string title,
        int expectedSubjectVersion,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateTopicCommand(
            SubjectId.From(subjectId),
            TopicId.From(topicId),
            title,
            code,
            expectedSubjectVersion);

        var result = await _updateTopicHandler.HandleAsync(command, cancellationToken);
        if (result.Status == ResultStatus.Conflict)
        {
            await RefreshSubjectAsync(subjectId, cancellationToken);
            HasTopicConflict = true;
            ConflictTopicId = topicId;
            ConflictTopicTitle = title;
            ConflictTopicCode = code;
            Response.StatusCode = StatusCodes.Status409Conflict;
            return Page();
        }

        if (!result.IsOk)
        {
            return this.Respond(result, "planning.update_topic");
        }

        return RedirectToPage("/Plans/Subject", new { planId, subjectId });
    }

    public async Task<IActionResult> OnPostMoveTopicAsync(
        Guid planId,
        Guid subjectId,
        Guid topicId,
        Guid? newParentId,
        int expectedSubjectVersion,
        CancellationToken cancellationToken = default)
    {
        var command = new MoveTopicCommand(
            SubjectId.From(subjectId),
            TopicId.From(topicId),
            newParentId.HasValue ? TopicId.From(newParentId.Value) : null,
            expectedSubjectVersion);

        var result = await _moveTopicHandler.HandleAsync(command, cancellationToken);

        return this.Respond(result, "planning.move_topic", options =>
        {
            options.RedirectPage = "/Plans/Subject";
            options.RedirectRouteValues = new { planId, subjectId };
        });
    }

    public async Task<IActionResult> OnPostReorderTopicAsync(
        Guid planId,
        Guid subjectId,
        Guid topicId,
        string direction,
        int expectedSubjectVersion,
        CancellationToken cancellationToken = default)
    {
        var currentTopic = Subject.Topics.FirstOrDefault(t => t.Id.Value == topicId);
        if (currentTopic is null)
        {
            return this.Respond(Result.NotFound(Message.Error("planning.topic.not_found")), "planning.reorder_topics");
        }

        var siblings = Subject.Topics
            .Where(t => t.ParentId == currentTopic.ParentId)
            .OrderBy(t => t.Order)
            .ToList();

        var index = siblings.FindIndex(t => t.Id.Value == topicId);
        var newIndex = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase) ? index - 1 : index + 1;
        if (newIndex >= 0 && newIndex < siblings.Count)
        {
            var item = siblings[index];
            siblings.RemoveAt(index);
            siblings.Insert(newIndex, item);

            var orderedIds = siblings.Select(t => t.Id).ToList();
            var reorderCommand = new ReorderTopicsCommand(
                SubjectId.From(subjectId),
                currentTopic.ParentId,
                orderedIds,
                expectedSubjectVersion);

            var reorderResult = await _reorderTopicsHandler.HandleAsync(reorderCommand, cancellationToken);
            if (!reorderResult.IsOk)
            {
                return this.Respond(reorderResult, "planning.reorder_topics");
            }
        }

        return RedirectToPage("/Plans/Subject", new { planId, subjectId });
    }

    public async Task<IActionResult> OnPostDeleteTopicAsync(
        Guid planId,
        Guid subjectId,
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteTopicCommand(SubjectId.From(subjectId), TopicId.From(topicId));
        var result = await _deleteTopicHandler.HandleAsync(command, cancellationToken);

        return this.Respond(result, "planning.delete_topic", options =>
        {
            options.RedirectPage = "/Plans/Subject";
            options.RedirectRouteValues = new { planId, subjectId };
        });
    }

    public async Task<IActionResult> OnGetDeleteTopicImpactAsync(
        Guid planId,
        Guid subjectId,
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetDeletionImpactQuery(
            DeletionTargetKind.Topic,
            StudyPlanId.From(planId),
            null,
            SubjectId.From(subjectId),
            TopicId.From(topicId));

        var result = await _getDeletionImpactHandler.HandleAsync(query, cancellationToken);
        if (!result.IsOk)
        {
            return this.Respond(result, "planning.get_topic_deletion_impact");
        }

        return new JsonResult(result.Data);
    }

    private static Guid ReadRouteGuid(PageHandlerExecutingContext context, string routeKey) =>
        Guid.Parse(Convert.ToString(context.RouteData.Values[routeKey], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);

    private async Task RefreshSubjectAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var subjectResult = await _getSubjectTopicsHandler.HandleAsync(
            new GetSubjectTopicsQuery(SubjectId.From(subjectId)),
            cancellationToken);

        if (subjectResult.IsOk)
        {
            Subject = subjectResult.Data;
            SubjectExpectedVersion = subjectResult.Data.Version;
        }
    }

    private void ShowSubject(SubjectDetailView subject)
    {
        Subject = subject;
        SubjectExpectedVersion = subject.Version;
        SubjectNameInput = subject.Name;
    }

    private void ShowNote(TopicNoteView note)
    {
        SelectedNote = note;
        NoteMarkdown = note.Markdown;
        NoteExpectedVersion = note.Version;
        RenderedMarkdownHtml = _markdownRenderer.RenderToHtml(note.Markdown);
    }
}
