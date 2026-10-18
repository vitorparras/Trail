using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Accounts;
using Trail.Domain.Common;
using Trail.Web.Results;

namespace Trail.Web.Pages.Account;

[AllowAnonymous]
public sealed class ConfirmEmailModel : PageModel
{
    private readonly ConfirmEmailHandler _confirmEmailHandler;

    public ConfirmEmailModel(ConfirmEmailHandler confirmEmailHandler)
    {
        ArgumentNullException.ThrowIfNull(confirmEmailHandler);
        _confirmEmailHandler = confirmEmailHandler;
    }

    public bool IsSuccess { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery] Guid? userId,
        [FromQuery] string? token,
        CancellationToken cancellationToken = default)
    {
        var command = new ConfirmEmailCommand(
            UserId.From(userId ?? Guid.Empty),
            token ?? string.Empty);

        var result = await _confirmEmailHandler.HandleAsync(command, cancellationToken);
        IsSuccess = result.IsOk;

        return this.Respond(result, "account.confirm_email");
    }
}
