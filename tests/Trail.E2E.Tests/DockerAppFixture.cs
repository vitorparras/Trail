using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Trail.E2E.Tests;

public sealed class DockerAppFixture : IAsyncLifetime
{
    internal const string ComposeOverridePath = "tests/Trail.E2E.Tests/compose.e2e.yml";

    private const string AdminEmail = "admin@trail.local";
    private const string AdminPassword = "AdminPass123!";
    private const string UserPassword = "UserPass123!";
    private const string ImageRepository = "trail-e2e";
    private const int ContainerPort = 8080;
    private const float ExpectTimeoutMilliseconds = 15_000;

    private readonly SemaphoreSlim _adminLock = new(1, 1);

    private string? _imageTag;
    private IContainer? _container;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private AppHttpSession? _admin;
    private string? _adminStorageState;

    internal static IReadOnlyDictionary<string, string> E2EEnvironment { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Registration__Mode"] = "InviteOnly",
        ["Security__AuthRateLimitPermitLimit"] = "1000",
    };

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
            _imageTag = $"{ImageRepository}:{Guid.NewGuid():N}";
            await RunDockerAsync($"build --file Dockerfile --tag {_imageTag} .");

            _container = new ContainerBuilder(_imageTag)
                .WithEnvironment("Database__Provider", "Sqlite")
                .WithEnvironment("ConnectionStrings__Default", "Data Source=/data/trail.db")
                .WithEnvironment(E2EEnvironment)
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

        _admin = await AppHttpSession.OpenAsync(_playwright, BaseUrl);
        await _admin.RegisterFirstAccountAsync(AdminEmail, AdminPassword);
        await _admin.SignInAsync(AdminEmail, AdminPassword);
        _adminStorageState = await _admin.StorageStateAsync();
    }

    public async Task<E2EUser> CreateUserAsync()
    {
        var email = $"user-{Guid.NewGuid():N}@trail.local";
        var invitationPath = await AsAdminAsync(admin => admin.CreateInvitationAsync(email));

        await using var visitor = await OpenSessionAsync();
        await visitor.RegisterInvitedAccountAsync(invitationPath, email, UserPassword);

        var confirmationPath = await AsAdminAsync(admin => admin.CreateConfirmationLinkAsync(email));
        await visitor.ConfirmEmailAsync(confirmationPath);
        await visitor.SignInAsync(email, UserPassword);

        return new E2EUser(email, UserPassword, await visitor.StorageStateAsync());
    }

    public Task<IPage> CreatePageAsync(BrowserNewContextOptions? options = null) =>
        NewPageAsync(options, storageState: null);

    public Task<IPage> CreatePageAsync(E2EUser user, BrowserNewContextOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        return NewPageAsync(options, user.StorageState);
    }

    public Task<IPage> CreatePageAsync(E2EUser user, int width, int height) =>
        CreatePageAsync(user, Viewport(width, height));

    public async Task<IPage> CreateOtherDevicePageAsync(E2EUser user, BrowserNewContextOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var device = await OpenSessionAsync();
        await device.SignInAsync(user.Email, user.Password);

        return await NewPageAsync(options, await device.StorageStateAsync());
    }

    public Task<IPage> CreateAdminPageAsync(int width, int height) =>
        NewPageAsync(
            Viewport(width, height),
            _adminStorageState ?? throw new InvalidOperationException("The administrator has not been bootstrapped."));

    public async ValueTask DisposeAsync()
    {
        if (_admin is not null)
        {
            await _admin.DisposeAsync();
        }

        if (_browser is not null)
        {
            await E2EBrowserContexts.CloseContextsOfBrowserAsync(_browser);
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        if (_imageTag is not null)
        {
            await RunDockerAsync($"image rm --force {_imageTag}");
        }

        _adminLock.Dispose();
    }

    private static BrowserNewContextOptions Viewport(int width, int height) => new()
    {
        ViewportSize = new ViewportSize { Width = width, Height = height },
    };

    private static async Task RunDockerAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            WorkingDirectory = RepositoryPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start docker {arguments}.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        _ = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"docker {arguments} failed with exit code {process.ExitCode}: {error}");
        }
    }

    private Task<IPage> NewPageAsync(BrowserNewContextOptions? options, string? storageState) =>
        E2EBrowserContexts.NewPageAsync(
            _browser ?? throw new InvalidOperationException("Browser has not been initialized."),
            options,
            storageState);

    private Task<AppHttpSession> OpenSessionAsync() =>
        AppHttpSession.OpenAsync(
            _playwright ?? throw new InvalidOperationException("Playwright has not been initialized."),
            BaseUrl);

    private async Task<T> AsAdminAsync<T>(Func<AppHttpSession, Task<T>> action)
    {
        var admin = _admin ?? throw new InvalidOperationException("The administrator has not been bootstrapped.");

        await _adminLock.WaitAsync();
        try
        {
            return await action(admin);
        }
        finally
        {
            _adminLock.Release();
        }
    }
}
