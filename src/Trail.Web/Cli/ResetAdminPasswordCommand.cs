using Trail.Application.Accounts;

namespace Trail.Web.Cli;

public static class ResetAdminPasswordCommand
{
    public static async Task<int> RunAsync(
        string[] args,
        IServiceProvider services,
        TextWriter? standardOut = null,
        TextWriter? standardError = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(services);

        standardOut ??= Console.Out;
        standardError ??= Console.Error;

        if (!TryParseArguments(args, out _, out _, out var validationError))
        {
            await standardError.WriteLineAsync(validationError);
            return 1;
        }

        using var scope = services.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IAccountIdentityGateway>();

        return await RunAsync(args, gateway, standardOut, standardError, cancellationToken);
    }

    public static async Task<int> RunAsync(
        string[] args,
        IAccountIdentityGateway gateway,
        TextWriter? standardOut = null,
        TextWriter? standardError = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(gateway);

        standardOut ??= Console.Out;
        standardError ??= Console.Error;

        if (!TryParseArguments(args, out var email, out var newPassword, out var validationError))
        {
            await standardError.WriteLineAsync(validationError);
            return 1;
        }

        var result = string.IsNullOrWhiteSpace(email)
            ? await gateway.ResetAdminPasswordAsync(newPassword, cancellationToken)
            : await gateway.ResetAdminPasswordAsync(email, newPassword, cancellationToken);

        if (!result.IsOk)
        {
            var errorMessage = result.Messages.Count > 0 ? result.Messages[0].Code : "accounts.reset_password.failed";
            await standardError.WriteLineAsync($"Error: {errorMessage}");
            return 1;
        }

        await standardOut.WriteLineAsync("Admin password has been successfully reset.");
        return 0;
    }

    private static bool TryParseArguments(
        string[] args,
        out string? email,
        out string newPassword,
        out string errorMessage)
    {
        email = null;
        newPassword = string.Empty;
        errorMessage = string.Empty;

        if (args.Length < 2)
        {
            errorMessage = "Error: New password is required. Usage: --reset-admin-password <new-password> or --reset-admin-password <email> <new-password>";
            return false;
        }

        if (args.Length == 2)
        {
            if (string.IsNullOrWhiteSpace(args[1]))
            {
                errorMessage = "Error: New password is required. Usage: --reset-admin-password <new-password> or --reset-admin-password <email> <new-password>";
                return false;
            }

            newPassword = args[1];
            return true;
        }

        if (string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            errorMessage = "Error: New password is required. Usage: --reset-admin-password <new-password> or --reset-admin-password <email> <new-password>";
            return false;
        }

        email = args[1];
        newPassword = args[2];
        return true;
    }
}
