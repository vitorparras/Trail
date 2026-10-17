using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Trail.Application.Accounts;
using Trail.Domain.Common;
using Trail.Web.Results;
using Trail.Web.Security;

namespace Trail.Web.Pages.Account;

[EnableRateLimiting(RateLimitingExtensions.AuthPolicyName)]
public sealed class ForgotPasswordModel : PageModel
{
    private readonly RequestPasswordResetHandler _forgotPasswordHandler;

    public ForgotPasswordModel(RequestPasswordResetHandler forgotPasswordHandler)
    {
        ArgumentNullException.ThrowIfNull(forgotPasswordHandler);
        _forgotPasswordHandler = forgotPasswordHandler;
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
            Url.Page("/Account/ResetPassword", pageHandler: null, values: new { userId = userId.Value, token }, protocol: Request.Scheme)
            ?? $"/Account/ResetPassword?userId={userId.Value}&token={Uri.EscapeDataString(token)}";

        var command = new RequestPasswordResetCommand(Email, LinkBuilder);
        var result = await _forgotPasswordHandler.HandleAsync(command, cancellationToken);

        if (!result.IsOk)
        {
            return this.Respond(result, "account.forgot_password");
        }

        Sent = true;
        return Page();
    }
}
