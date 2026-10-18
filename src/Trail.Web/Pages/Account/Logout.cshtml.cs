using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Accounts;
using Trail.Web.Results;

namespace Trail.Web.Pages.Account;

[AllowAnonymous]
public sealed class LogoutModel : PageModel
{
    private readonly LogoutHandler _logoutHandler;

    public LogoutModel(LogoutHandler logoutHandler)
    {
        ArgumentNullException.ThrowIfNull(logoutHandler);
        _logoutHandler = logoutHandler;
    }

    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        var result = await _logoutHandler.HandleAsync(new LogoutCommand(), cancellationToken);
        return this.Respond(result, "account.logout", options =>
        {
            options.RedirectPage = "/Index";
        });
    }
}
