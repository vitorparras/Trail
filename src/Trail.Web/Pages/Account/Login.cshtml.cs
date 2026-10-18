using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Trail.Application.Abstractions;
using Trail.Application.Accounts;
using Trail.Web.Results;
using Trail.Web.Security;

namespace Trail.Web.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting(RateLimitingExtensions.AuthPolicyName)]
public sealed class LoginModel : PageModel
{
    private static readonly FieldTargetMap Fields = FieldTargetMap.Empty.Prefixed(
        nameof(Input),
        nameof(InputModel.Email),
        nameof(InputModel.Password));

    private readonly LoginHandler _loginHandler;
    private readonly ICurrentUser _currentUser;

    public LoginModel(LoginHandler loginHandler, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(loginHandler);
        ArgumentNullException.ThrowIfNull(currentUser);

        _loginHandler = loginHandler;
        _currentUser = currentUser;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [FromQuery]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (_currentUser.IsAuthenticated)
        {
            var destination = !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
                ? ReturnUrl
                : "/";
            return LocalRedirect(destination);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        var command = new LoginCommand(Input.Email, Input.Password, Input.RememberMe);
        var result = await _loginHandler.HandleAsync(command, cancellationToken);

        if (!result.IsOk)
        {
            return this.Respond(result, "account.login", options => options.Fields = Fields);
        }

        var destination = !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl
            : "/";

        return LocalRedirect(destination);
    }

    public sealed class InputModel
    {
        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
