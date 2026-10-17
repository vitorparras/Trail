using System.Diagnostics;
using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Trail.E2E.Tests;

public sealed partial class DockerAppFixture : IAsyncLifetime
{
    public const string AdminEmail = "admin@trail.local";
    public const string AdminPassword = "AdminPass123!";

    private const string ImageName = "ghcr.io/vitorparras/trail:latest";
    private const int ContainerPort = 8080;
    private const float ExpectTimeoutMilliseconds = 15_000;

    private static readonly SemaphoreSlim BuildLock = new(1, 1);
    private static bool _imageBuilt;

    private IContainer? _container;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private string? _adminStorageState;

    public string BaseUrl { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        SetDefaultExpectTimeout(ExpectTimeoutMilliseconds);

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

        _adminStorageState = await BootstrapAdminAsync(_browser);
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

    public Task<IPage> CreateAdminPageAsync(int width, int height) =>
        CreatePageAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = width, Height = height },
            StorageState = _adminStorageState ?? throw new InvalidOperationException("The administrator has not been bootstrapped."),
        });

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

    private async Task<string> BootstrapAdminAsync(IBrowser browser)
    {
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
        });

        var page = await context.NewPageAsync();
        await page.GotoAsync(BaseUrl + "/Account/Register");

        var main = page.GetByRole(AriaRole.Main);
        await main.GetByLabel(UiText.Get("Email"), new() { Exact = true }).FillAsync(AdminEmail);
        await main.GetByLabel(UiText.Get("Password"), new() { Exact = true }).FillAsync(AdminPassword);
        await main.GetByLabel(UiText.Get("ConfirmPassword"), new() { Exact = true }).FillAsync(AdminPassword);

        var registration = await page.RunAndWaitForResponseAsync(
            () => main.GetByRole(AriaRole.Button, new() { Name = UiText.Get("Register"), Exact = true }).ClickAsync(),
            response => response.Request.Method == "POST" && response.Url.Contains("/Account/Register", StringComparison.Ordinal));

        if (registration.Status != 302)
        {
            throw new InvalidOperationException(
                $"The first account could not be registered (HTTP {registration.Status}). The end-to-end suite needs a fresh instance.");
        }

        await Expect(page).ToHaveURLAsync(LoginPathPattern());
        await Expect(main.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(UiText.Get("LoginHeading"));

        await main.GetByLabel(UiText.Get("Email"), new() { Exact = true }).FillAsync(AdminEmail);
        await main.GetByLabel(UiText.Get("Password"), new() { Exact = true }).FillAsync(AdminPassword);
        await main.GetByLabel(UiText.Get("RememberMe"), new() { Exact = true }).CheckAsync();
        await main.GetByRole(AriaRole.Button, new() { Name = UiText.Get("Login"), Exact = true }).ClickAsync();

        await Expect(page).Not.ToHaveURLAsync(LoginPathPattern());
        await Expect(page.GetByRole(AriaRole.Banner).GetByRole(AriaRole.Link, new() { Name = UiText.Get("Administration"), Exact = true }))
            .ToBeVisibleAsync();

        return await context.StorageStateAsync();
    }

    private static async Task EnsureImageBuiltAsync()
    {
        if (_imageBuilt)
        {
            return;
        }

        await BuildLock.WaitAsync();
        try
        {
            if (_imageBuilt)
            {
                return;
            }

            var buildStartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"build --file Dockerfile --tag {ImageName} .",
                WorkingDirectory = RepositoryPaths.Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var buildProcess = Process.Start(buildStartInfo)
                ?? throw new InvalidOperationException("Failed to start docker build process.");

            var outputTask = buildProcess.StandardOutput.ReadToEndAsync();
            var errorTask = buildProcess.StandardError.ReadToEndAsync();

            await buildProcess.WaitForExitAsync();
            _ = await outputTask;
            var error = await errorTask;

            if (buildProcess.ExitCode != 0)
            {
                throw new InvalidOperationException($"docker build failed with exit code {buildProcess.ExitCode}: {error}");
            }

            _imageBuilt = true;
        }
        finally
        {
            BuildLock.Release();
        }
    }

    [GeneratedRegex("/Account/Login(\\?.*)?$")]
    private static partial Regex LoginPathPattern();
}
