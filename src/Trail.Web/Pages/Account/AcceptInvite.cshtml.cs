using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Trail.Web.Pages.Account;

public sealed class AcceptInviteModel : PageModel
{
    public IActionResult OnGet([FromQuery] string? token = null, [FromQuery] string? inviteToken = null)
    {
        var effectiveToken = !string.IsNullOrWhiteSpace(token) ? token : inviteToken;
        return RedirectToPage("/Account/Register", new { inviteToken = effectiveToken });
    }
}
