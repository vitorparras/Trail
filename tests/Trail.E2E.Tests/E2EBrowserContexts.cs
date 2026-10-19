using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace Trail.E2E.Tests;

internal static class E2EBrowserContexts
{
    public const string AuthenticationCookieName = ".AspNetCore.Identity.Application";

    private static readonly ConcurrentDictionary<IBrowserContext, string?> Open = new();

    public static async Task<IPage> NewPageAsync(IBrowser browser, BrowserNewContextOptions? options = null, string? storageState = null)
    {
        ArgumentNullException.ThrowIfNull(browser);

        var testId = TestContext.Current.Test?.UniqueID;
        await CloseAsync(entry => entry.Key.Browser == browser && entry.Value is not null && entry.Value != testId);

        var contextOptions = options is null ? new BrowserNewContextOptions() : new BrowserNewContextOptions(options);
        contextOptions.ServiceWorkers ??= ServiceWorkerPolicy.Block;
        if (storageState is not null)
        {
            contextOptions.StorageState = storageState;
        }

        var context = await browser.NewContextAsync(contextOptions);
        Open[context] = testId;
        context.Close += (sender, closed) => Open.TryRemove(closed, out _);

        return await context.NewPageAsync();
    }

    public static async Task<string?> AuthenticationCookieAsync(IBrowserContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cookies = await context.CookiesAsync();
        return cookies.SingleOrDefault(cookie => cookie.Name == AuthenticationCookieName)?.Value;
    }

    public static Task CloseContextsOfBrowserAsync(IBrowser browser) =>
        CloseAsync(entry => entry.Key.Browser == browser);

    private static async Task CloseAsync(Func<KeyValuePair<IBrowserContext, string?>, bool> predicate)
    {
        foreach (var context in Open.Where(predicate).Select(entry => entry.Key).ToList())
        {
            if (Open.TryRemove(context, out _) && context.Browser?.IsConnected != false)
            {
                await context.CloseAsync();
            }
        }
    }
}
