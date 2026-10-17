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

        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            await standardError.WriteLineAsync("Error: New password is required. Usage: --reset-admin-password <new-password>");
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

        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            await standardError.WriteLineAsync("Error: New password is required. Usage: --reset-admin-password <new-password>");
            return 1;
        }

        var newPassword = args[1];
        var result = await gateway.ResetAdminPasswordAsync(newPassword, cancellationToken);

        if (!result.IsOk)
        {
            var errorMessage = result.Messages.Count > 0 ? result.Messages[0].Code : "accounts.reset_password.failed";
            await standardError.WriteLineAsync($"Error: {errorMessage}");
            return 1;
        }

        await standardOut.WriteLineAsync("Admin password has been successfully reset.");
        return 0;
    }
}
