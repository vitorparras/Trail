using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Trail.Infrastructure.Persistence;

public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    private readonly IConfiguration _configuration;

    public DatabaseOptionsValidator(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var rawProvider = _configuration[$"{DatabaseOptions.SectionName}:Provider"];
        if (string.IsNullOrWhiteSpace(rawProvider))
        {
            return ValidateOptionsResult.Fail("Configuration 'Database:Provider' is required and must be either 'Sqlite' or 'PostgreSql'.");
        }

        if (!Enum.TryParse<DatabaseProvider>(rawProvider, ignoreCase: true, out _))
        {
            return ValidateOptionsResult.Fail($"Configuration 'Database:Provider' value '{rawProvider}' is invalid. Allowed values are 'Sqlite' or 'PostgreSql'.");
        }

        var connectionString = options.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return ValidateOptionsResult.Fail("Connection string 'ConnectionStrings:Default' is required and cannot be empty or whitespace.");
        }

        return ValidateOptionsResult.Success;
    }
}
