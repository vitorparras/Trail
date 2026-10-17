using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Web.Htmx;
using Trail.Web.Results;

namespace Trail.Web.Pages;

[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public sealed class ErrorModel : PageModel
{
    public string? TraceId { get; set; }

    public IActionResult OnGet() => Execute();

    public IActionResult OnPost() => Execute();

    public ErrorViewModel ToViewModel() => new(TraceId);

    private IActionResult Execute()
    {
        TraceId = Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
        Response.StatusCode = StatusCodes.Status500InternalServerError;

        if (Request.IsHtmxPartialRequest())
        {
            return Partial("_Error", ToViewModel());
        }

        return Page();
    }
}
