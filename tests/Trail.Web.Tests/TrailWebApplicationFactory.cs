using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Trail.Web.Tests;

public class TrailWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseDirectory;
    private readonly string _databasePath;

    public TrailWebApplicationFactory()
    {
        _databaseDirectory = Path.Combine(Path.GetTempPath(), "TrailWebTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_databaseDirectory);
        _databasePath = Path.Combine(_databaseDirectory, "trail_test.db");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_databaseDirectory))
        {
            try
            {
                SqliteConnection.ClearAllPools();
                Directory.Delete(_databaseDirectory, recursive: true);
            }
            catch
            {
            }
        }
    }
}
