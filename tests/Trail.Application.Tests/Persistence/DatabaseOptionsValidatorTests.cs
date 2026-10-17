using Microsoft.Extensions.Configuration;
using Trail.Infrastructure.Persistence;

namespace Trail.Application.Tests.Persistence;

public sealed class DatabaseOptionsValidatorTests
{
    [Fact]
    public void Validate_WhenProviderIsMissing_FailsWithClearMessage()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Data Source=trail.db",
            })
            .Build();

        var validator = new DatabaseOptionsValidator(configuration);
        var options = new DatabaseOptions { ConnectionString = "Data Source=trail.db" };

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Database:Provider", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("required", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenProviderIsInvalid_FailsWithClearMessage()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Oracle",
                ["ConnectionStrings:Default"] = "Data Source=trail.db",
            })
            .Build();

        var validator = new DatabaseOptionsValidator(configuration);
        var options = new DatabaseOptions { ConnectionString = "Data Source=trail.db" };

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Database:Provider", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Oracle", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Allowed values are 'Sqlite' or 'PostgreSql'", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenConnectionStringIsMissingOrWhitespace_FailsWithClearMessage(string? connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
            })
            .Build();

        var validator = new DatabaseOptionsValidator(configuration);
        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.Sqlite,
            ConnectionString = connectionString!,
        };

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ConnectionStrings:Default", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("required", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Sqlite", DatabaseProvider.Sqlite)]
    [InlineData("PostgreSql", DatabaseProvider.PostgreSql)]
    public void Validate_WhenConfigurationIsValid_Succeeds(string providerName, DatabaseProvider expectedProvider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = providerName,
                ["ConnectionStrings:Default"] = "Data Source=trail.db",
            })
            .Build();

        var validator = new DatabaseOptionsValidator(configuration);
        var options = new DatabaseOptions
        {
            Provider = expectedProvider,
            ConnectionString = "Data Source=trail.db",
        };

        var result = validator.Validate(null, options);

        Assert.False(result.Failed);
    }
}
