using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Accounts;
using Trail.Domain.Common;
using Trail.Web.Results;

namespace Trail.Web.Pages.Admin;

public sealed class UsersModel : PageModel
{
    private static readonly FieldTargetMap Fields = FieldTargetMap.Empty
        .Field(nameof(CreateInvitationCommand.Email), "email")
        .Field(nameof(SendTestEmailCommand.TargetEmail), "recipientEmail");

    private readonly ListUsersHandler _listUsersHandler;
    private readonly SetUserActiveStatusHandler _setActiveStatusHandler;
    private readonly CreateInvitationHandler _createInvitationHandler;
    private readonly GenerateManualAccountLinkHandler _generateManualLinkHandler;
    private readonly SendTestEmailHandler _sendTestEmailHandler;

    public UsersModel(
        ListUsersHandler listUsersHandler,
        SetUserActiveStatusHandler setActiveStatusHandler,
        CreateInvitationHandler createInvitationHandler,
        GenerateManualAccountLinkHandler generateManualLinkHandler,
        SendTestEmailHandler sendTestEmailHandler)
    {
        ArgumentNullException.ThrowIfNull(listUsersHandler);
        ArgumentNullException.ThrowIfNull(setActiveStatusHandler);
        ArgumentNullException.ThrowIfNull(createInvitationHandler);
        ArgumentNullException.ThrowIfNull(generateManualLinkHandler);
        ArgumentNullException.ThrowIfNull(sendTestEmailHandler);

        _listUsersHandler = listUsersHandler;
        _setActiveStatusHandler = setActiveStatusHandler;
        _createInvitationHandler = createInvitationHandler;
        _generateManualLinkHandler = generateManualLinkHandler;
        _sendTestEmailHandler = sendTestEmailHandler;
    }

    public IReadOnlyList<AccountUserDto> Users { get; private set; } = [];

    public IReadOnlyList<AccountInvitationDto> Invitations { get; private set; } = [];

    public RegistrationMode RegistrationMode { get; private set; } = RegistrationMode.Closed;

    public bool AcceptsInvitations => RegistrationMode != RegistrationMode.Closed;

    [TempData]
    public string? GeneratedInviteLink { get; set; }

    [TempData]
    public string? GeneratedManualLink { get; set; }

    [TempData]
    public string? ManualLinkKindName { get; set; }

    [TempData]
    public bool TestEmailSuccess { get; set; }

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (!context.ModelState.IsValid)
        {
            await LoadOverviewAsync(context.HttpContext.RequestAborted);
        }

        await next();
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        var result = await _listUsersHandler.HandleAsync(new ListUsersQuery(), cancellationToken);

        if (!result.IsOk)
        {
            return this.Respond(result, "admin.list_users");
        }

        Users = result.Data.Users;
        Invitations = result.Data.Invitations;
        RegistrationMode = result.Data.RegistrationMode;

        return Page();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var command = new SetUserActiveStatusCommand(UserId.From(userId), isActive);
        var result = await _setActiveStatusHandler.HandleAsync(command, cancellationToken);

        if (!result.IsOk)
        {
            await LoadOverviewAsync(cancellationToken);
            return this.Respond(result, "admin.toggle_active", options => options.Fields = Fields);
        }

        return RedirectToPage("/Admin/Users");
    }

    public async Task<IActionResult> OnPostInviteAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        string LinkBuilder(string token) =>
            Url.Page("/Account/Register", pageHandler: null, values: new { inviteToken = token }, protocol: Request.Scheme)
            ?? $"/Account/Register?inviteToken={Uri.EscapeDataString(token)}";

        var command = new CreateInvitationCommand(email, LinkBuilder);
        var result = await _createInvitationHandler.HandleAsync(command, cancellationToken);

        if (!result.IsOk)
        {
            await LoadOverviewAsync(cancellationToken);
            return this.Respond(result, "admin.create_invite", options => options.Fields = Fields);
        }

        GeneratedInviteLink = result.Data.InvitationLink;
        return RedirectToPage("/Admin/Users");
    }

    public async Task<IActionResult> OnPostManualLinkAsync(
        Guid userId,
        ManualLinkKind kind,
        CancellationToken cancellationToken = default)
    {
        string LinkBuilder(UserId targetUserId, string token) =>
            kind == ManualLinkKind.EmailConfirmation
                ? (Url.Page("/Account/ConfirmEmail", pageHandler: null, values: new { userId = targetUserId.Value, token }, protocol: Request.Scheme)
                   ?? $"/Account/ConfirmEmail?userId={targetUserId.Value}&token={Uri.EscapeDataString(token)}")
                : (Url.Page("/Account/ResetPassword", pageHandler: null, values: new { userId = targetUserId.Value, token }, protocol: Request.Scheme)
                   ?? $"/Account/ResetPassword?userId={targetUserId.Value}&token={Uri.EscapeDataString(token)}");

        var command = new GenerateManualAccountLinkCommand(UserId.From(userId), kind, LinkBuilder);
        var result = await _generateManualLinkHandler.HandleAsync(command, cancellationToken);

        if (!result.IsOk)
        {
            await LoadOverviewAsync(cancellationToken);
            return this.Respond(result, "admin.manual_link", options => options.Fields = Fields);
        }

        GeneratedManualLink = result.Data.Url;
        ManualLinkKindName = kind.ToString();
        return RedirectToPage("/Admin/Users");
    }

    public async Task<IActionResult> OnPostTestEmailAsync(
        string recipientEmail,
        CancellationToken cancellationToken = default)
    {
        var command = new SendTestEmailCommand(recipientEmail);
        var result = await _sendTestEmailHandler.HandleAsync(command, cancellationToken);

        if (!result.IsOk)
        {
            await LoadOverviewAsync(cancellationToken);
            return this.Respond(result, "admin.send_test_email", options => options.Fields = Fields);
        }

        TestEmailSuccess = true;
        return RedirectToPage("/Admin/Users");
    }

    private async Task LoadOverviewAsync(CancellationToken cancellationToken)
    {
        var listResult = await _listUsersHandler.HandleAsync(new ListUsersQuery(), cancellationToken);
        if (listResult.IsOk)
        {
            Users = listResult.Data.Users;
            Invitations = listResult.Data.Invitations;
            RegistrationMode = listResult.Data.RegistrationMode;
        }
    }
}
