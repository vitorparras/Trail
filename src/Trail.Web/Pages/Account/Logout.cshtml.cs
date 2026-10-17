using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Accounts;

namespace Trail.Web.Pages.Account;

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
        await _logoutHandler.HandleAsync(new LogoutCommand(), cancellationToken);
        return RedirectToPage("/Index");
    }
}
