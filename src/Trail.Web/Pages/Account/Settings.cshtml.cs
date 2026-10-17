using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Trail.Application.Accounts;
using Trail.Web.Results;

namespace Trail.Web.Pages.Account;

public sealed class SettingsModel : PageModel
{
    private readonly GetUserSettingsHandler _getSettingsHandler;
    private readonly UpdateUserSettingsHandler _updateSettingsHandler;
    private readonly ChangePasswordHandler _changePasswordHandler;

    public SettingsModel(
        GetUserSettingsHandler getSettingsHandler,
        UpdateUserSettingsHandler updateSettingsHandler,
        ChangePasswordHandler changePasswordHandler)
    {
        ArgumentNullException.ThrowIfNull(getSettingsHandler);
        ArgumentNullException.ThrowIfNull(updateSettingsHandler);
        ArgumentNullException.ThrowIfNull(changePasswordHandler);

        _getSettingsHandler = getSettingsHandler;
        _updateSettingsHandler = updateSettingsHandler;
        _changePasswordHandler = changePasswordHandler;
    }

    [BindProperty]
    public SettingsInputModel SettingsInput { get; set; } = new();

    [BindProperty]
    public PasswordInputModel PasswordInput { get; set; } = new();

    public static readonly IReadOnlyList<string> AvailableTimeZones =
    [
        "America/Sao_Paulo",
        "UTC",
        "America/New_York",
        "America/Chicago",
        "America/Denver",
        "America/Los_Angeles",
        "America/Buenos_Aires",
        "Europe/London",
        "Europe/Paris",
        "Europe/Berlin",
        "Europe/Lisbon",
        "Europe/Madrid",
        "Asia/Tokyo",
        "Asia/Shanghai",
        "Australia/Sydney"
    ];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        var result = await _getSettingsHandler.HandleAsync(new GetUserSettingsQuery(), cancellationToken);

        if (!result.IsOk)
        {
            return this.Respond(result, "account.get_settings");
        }

        SettingsInput = new SettingsInputModel
        {
            TimeZoneId = result.Data.TimeZoneId,
            PomodoroFocusMinutes = result.Data.PomodoroFocusMinutes,
            PomodoroBreakMinutes = result.Data.PomodoroBreakMinutes,
        };

        return Page();
    }

    public async Task<IActionResult> OnPostSettingsAsync(CancellationToken cancellationToken = default)
    {
        var command = new UpdateUserSettingsCommand(
            SettingsInput.TimeZoneId,
            SettingsInput.PomodoroFocusMinutes,
            SettingsInput.PomodoroBreakMinutes);

        var result = await _updateSettingsHandler.HandleAsync(command, cancellationToken);

        return this.Respond(result, "account.update_settings", options =>
        {
            options.PartialViewName = "_SettingsForm";
            options.PartialModelFactory = dto => new SettingsInputModel
            {
                TimeZoneId = dto.TimeZoneId,
                PomodoroFocusMinutes = dto.PomodoroFocusMinutes,
                PomodoroBreakMinutes = dto.PomodoroBreakMinutes,
            };
            options.RedirectPage = "/Account/Settings";
        });
    }

    public async Task<IActionResult> OnPostPasswordAsync(CancellationToken cancellationToken = default)
    {
        var command = new ChangePasswordCommand(
            PasswordInput.CurrentPassword,
            PasswordInput.NewPassword,
            PasswordInput.ConfirmPassword);

        var result = await _changePasswordHandler.HandleAsync(command, cancellationToken);

        return this.Respond(result, "account.change_password", options =>
        {
            options.PartialViewName = "_PasswordForm";
            options.PartialModel = new PasswordInputModel();
            options.RedirectPage = "/Account/Settings";
        });
    }

    public sealed class SettingsInputModel
    {
        public string TimeZoneId { get; set; } = "America/Sao_Paulo";

        public int PomodoroFocusMinutes { get; set; } = 25;

        public int PomodoroBreakMinutes { get; set; } = 5;
    }

    public sealed class PasswordInputModel
    {
        public string CurrentPassword { get; set; } = string.Empty;

        public string NewPassword { get; set; } = string.Empty;

        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
