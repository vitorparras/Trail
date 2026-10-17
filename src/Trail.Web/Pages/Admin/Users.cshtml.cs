using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Accounts;
using Trail.Domain.Common;
using Trail.Web.Results;

namespace Trail.Web.Pages.Admin;

public sealed class UsersModel : PageModel
{
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

    [TempData]
    public string? GeneratedInviteLink { get; set; }

    [TempData]
    public string? GeneratedManualLink { get; set; }

    [TempData]
    public string? ManualLinkKindName { get; set; }

    [TempData]
    public bool TestEmailSuccess { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        var result = await _listUsersHandler.HandleAsync(new ListUsersQuery(), cancellationToken);

        if (!result.IsOk)
        {
            return this.Respond(result, "admin.list_users");
        }

        Users = result.Data.Users;
        Invitations = result.Data.Invitations;

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
            return this.Respond(result, "admin.toggle_active");
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
            return this.Respond(result, "admin.create_invite");
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
            return this.Respond(result, "admin.manual_link");
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
            return this.Respond(result, "admin.send_test_email");
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
        }
    }
}
