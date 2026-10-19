using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Accounts;
using Trail.Domain.Common;
using Trail.Web.Results;

namespace Trail.Web.Pages.Account;

[AllowAnonymous]
public sealed class RegisterModel : PageModel
{
    private static readonly FieldTargetMap Fields = FieldTargetMap.Empty.Prefixed(
        nameof(Input),
        nameof(InputModel.Email),
        nameof(InputModel.Password),
        nameof(InputModel.ConfirmPassword));

    private readonly RegisterAccountHandler _registerHandler;

    public RegisterModel(RegisterAccountHandler registerHandler)
    {
        ArgumentNullException.ThrowIfNull(registerHandler);
        _registerHandler = registerHandler;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool ConfirmationSent { get; set; }

    public void OnGet([FromQuery] string? inviteToken = null, [FromQuery] string? email = null)
    {
        if (!string.IsNullOrWhiteSpace(inviteToken))
        {
            Input.InviteToken = inviteToken;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            Input.Email = email;
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        string LinkBuilder(UserId userId, string token) =>
            Url.Page("/Account/ConfirmEmail", pageHandler: null, values: new { userId = userId.Value, token }, protocol: Request.Scheme)
            ?? $"/Account/ConfirmEmail?userId={userId.Value}&token={Uri.EscapeDataString(token)}";

        var command = new RegisterAccountCommand(
            Input.Email,
            Input.Password,
            Input.ConfirmPassword,
            Input.InviteToken,
            LinkBuilder,
            Url.Page("/Account/ForgotPassword", pageHandler: null, values: null, protocol: Request.Scheme));

        var result = await _registerHandler.HandleAsync(command, cancellationToken);

        if (!result.IsOk)
        {
            return this.Respond(result, "account.register", options => options.Fields = Fields);
        }

        if (result.Data.RequiresEmailConfirmation)
        {
            ConfirmationSent = true;
            return Page();
        }

        return RedirectToPage("/Account/Login");
    }

    public sealed class InputModel
    {
        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string ConfirmPassword { get; set; } = string.Empty;

        public string? InviteToken { get; set; }
    }
}
