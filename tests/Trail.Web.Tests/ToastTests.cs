using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Localization;
using Trail.Domain.Common;
using Trail.Web.Toasts;

namespace Trail.Web.Tests;

public sealed class ToastTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Toasts_Partial_AppendsToastsOutOfBandToTheContainer()
    {
        Toast[] toasts =
        [
            new(MessageSeverity.Error, "error text"),
            new(MessageSeverity.Warning, "warning text"),
            new(MessageSeverity.Info, "info text"),
            new(MessageSeverity.Success, "success text"),
        ];

        using var document = await PartialRenderer.RenderAsync(factory.Services, "/Pages/Shared/_Toasts.cshtml", toasts);

        var wrapper = Assert.Single(document.QuerySelectorAll("[hx-swap-oob]"));
        Assert.Equal("beforeend:#toasts", wrapper.GetAttribute("hx-swap-oob"));
        Assert.Equal(["error", "warning", "info", "success"], wrapper.Children.Select(toast => toast.GetAttribute("data-toast")));
        Assert.Equal(["true", "true", "false", "false"], wrapper.Children.Select(toast => toast.GetAttribute("data-toast-persistent")));
        Assert.Equal(toasts.Select(toast => toast.Text), wrapper.Children.Select(toast => toast.QuerySelector("span")!.TextContent));
    }

    [Fact]
    public async Task Toast_Partial_HasAnAccessibleDismissButton()
    {
        var dismiss = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>()["DismissNotification"].Value;

        using var document = await PartialRenderer.RenderAsync(factory.Services, "/Pages/Shared/_Toast.cshtml", new Toast(MessageSeverity.Success, "saved"));

        var button = document.QuerySelector("[data-toast] button[type='button'][data-toast-dismiss]");
        Assert.NotNull(button);
        Assert.Equal(dismiss, button.QuerySelector(".sr-only")!.TextContent);
        Assert.Equal("true", button.QuerySelector("svg")!.GetAttribute("aria-hidden"));
    }

    [Fact]
    public async Task Get_Home_ProvidesTheUnexpectedErrorToastTemplate()
    {
        var unexpectedError = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>()["UnexpectedError"];
        Assert.False(unexpectedError.ResourceNotFound);
        using var client = HomePage.CreateClient(factory);
        using var document = await HomePage.LoadAsync(client);

        var template = document.QuerySelector("template#toast-unexpected-error") as AngleSharp.Html.Dom.IHtmlTemplateElement;

        Assert.NotNull(template);
        var toast = template.Content.QuerySelector("[data-toast='error'][data-toast-persistent='true']");
        Assert.NotNull(toast);
        Assert.Equal(unexpectedError.Value, toast.QuerySelector("span")!.TextContent);
    }
}
