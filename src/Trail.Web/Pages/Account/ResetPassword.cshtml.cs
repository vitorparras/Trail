using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Accounts;
using Trail.Domain.Common;
using Trail.Web.Results;

namespace Trail.Web.Pages.Account;

public sealed class ResetPasswordModel : PageModel
{
    private readonly ResetPasswordHandler _resetPasswordHandler;

    public ResetPasswordModel(ResetPasswordHandler resetPasswordHandler)
    {
        ArgumentNullException.ThrowIfNull(resetPasswordHandler);
        _resetPasswordHandler = resetPasswordHandler;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool ResetComplete { get; private set; }

    public void OnGet([FromQuery] Guid? userId, [FromQuery] string? token)
    {
        if (userId.HasValue)
        {
            Input.UserId = userId.Value;
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            Input.Token = token;
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        var command = new ResetPasswordCommand(
            UserId.From(Input.UserId),
            Input.Token,
            Input.NewPassword,
            Input.ConfirmNewPassword);

        var result = await _resetPasswordHandler.HandleAsync(command, cancellationToken);

        if (!result.IsOk)
        {
            return this.Respond(result, "account.reset_password");
        }

        ResetComplete = true;
        return Page();
    }

    public sealed class InputModel
    {
        private string _confirmPassword = string.Empty;

        public Guid UserId { get; set; }

        public string Token { get; set; } = string.Empty;

        public string NewPassword { get; set; } = string.Empty;

        public string ConfirmNewPassword
        {
            get => _confirmPassword;
            set => _confirmPassword = value;
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => _confirmPassword = value;
        }
    }
}
