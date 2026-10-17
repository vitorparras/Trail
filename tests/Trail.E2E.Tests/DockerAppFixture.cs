using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Playwright;

namespace Trail.E2E.Tests;

public sealed class DockerAppFixture : IAsyncLifetime
{
    private const string ImageName = "ghcr.io/vitorparras/trail:latest";
    private const int ContainerPort = 8080;

    private IContainer? _container;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public string BaseUrl { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var envBaseUrl = Environment.GetEnvironmentVariable("TRAIL_E2E_BASE_URL");
        if (!string.IsNullOrWhiteSpace(envBaseUrl))
        {
            BaseUrl = envBaseUrl.TrimEnd('/');
        }
        else
        {
            await EnsureImageBuiltAsync();

            _container = new ContainerBuilder(ImageName)
                .WithEnvironment("Database__Provider", "Sqlite")
                .WithEnvironment("ConnectionStrings__Default", "Data Source=/data/trail.db")
                .WithPortBinding(ContainerPort, assignRandomHostPort: true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
                    request.ForPath("/health/ready").ForPort(ContainerPort)))
                .Build();

            await _container.StartAsync();

            var mappedPort = _container.GetMappedPublicPort(ContainerPort);
            BaseUrl = $"http://{_container.Hostname}:{mappedPort}";
        }

        var installExitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (installExitCode != 0)
        {
            throw new InvalidOperationException($"Playwright browser install failed with exit code {installExitCode}.");
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task<IPage> CreatePageAsync(BrowserNewContextOptions? options = null)
    {
        if (_browser is null)
        {
            throw new InvalidOperationException("Browser has not been initialized.");
        }

        var context = await _browser.NewContextAsync(options);
        return await context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private static async Task EnsureImageBuiltAsync()
    {
        var repositoryRoot = FindRepositoryRoot();
        var buildStartInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"build --file Dockerfile --tag {ImageName} .",
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var buildProcess = Process.Start(buildStartInfo);
        if (buildProcess is null)
        {
            throw new InvalidOperationException("Failed to start docker build process.");
        }

        var outputTask = buildProcess.StandardOutput.ReadToEndAsync();
        var errorTask = buildProcess.StandardError.ReadToEndAsync();

        await buildProcess.WaitForExitAsync();
        _ = await outputTask;
        var error = await errorTask;

        if (buildProcess.ExitCode != 0)
        {
            throw new InvalidOperationException($"docker build failed with exit code {buildProcess.ExitCode}: {error}");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Trail.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root containing Trail.slnx was not found.");
    }
}
