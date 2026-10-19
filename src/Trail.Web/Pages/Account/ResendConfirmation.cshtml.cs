using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Accounts;
using Trail.Domain.Common;
using Trail.Web.Results;

namespace Trail.Web.Pages.Account;

[AllowAnonymous]
public sealed class ResendConfirmationModel : PageModel
{
    private readonly ResendEmailConfirmationHandler _resendHandler;

    public ResendConfirmationModel(ResendEmailConfirmationHandler resendHandler)
    {
        ArgumentNullException.ThrowIfNull(resendHandler);
        _resendHandler = resendHandler;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    public bool Sent { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        string LinkBuilder(UserId userId, string token) =>
            Url.Page("/Account/ConfirmEmail", pageHandler: null, values: new { userId = userId.Value, token }, protocol: Request.Scheme)
            ?? $"/Account/ConfirmEmail?userId={userId.Value}&token={Uri.EscapeDataString(token)}";

        var command = new ResendEmailConfirmationCommand(Email, LinkBuilder);
        var result = await _resendHandler.HandleAsync(command, cancellationToken);

        if (!result.IsOk)
        {
            return this.Respond(result, "account.resend_confirmation");
        }

        Sent = true;
        return Page();
    }
}
