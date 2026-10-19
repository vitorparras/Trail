using Microsoft.AspNetCore.Mvc.ModelBinding;
using Trail.Web.Forms;

namespace Trail.Web.Tests.Forms;

public sealed class FormErrorsTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    private const string PartialPath = "/Pages/Shared/_FormErrors.cshtml";

    [Fact]
    public async Task Partial_WithErrors_RendersAnAlertListWithTheGivenId()
    {
        var model = new FormErrorList("title-errors", ["First problem", "Second problem"]);

        using var document = await PartialRenderer.RenderAsync(factory.Services, PartialPath, model);

        var list = document.GetElementById("title-errors");
        Assert.NotNull(list);
        Assert.Single(list.QuerySelectorAll("ul"));
        Assert.Equal("alert", list.GetAttribute("role"));
        Assert.Equal(["First problem", "Second problem"], list.QuerySelectorAll("li").Select(item => item.TextContent));
    }

    [Fact]
    public async Task Partial_WithoutErrors_RendersNothing()
    {
        using var document = await PartialRenderer.RenderAsync(factory.Services, PartialPath, new FormErrorList("empty-errors", []));

        Assert.Null(document.GetElementById("empty-errors"));
        Assert.Empty(document.Body!.Children);
    }

    [Fact]
    public async Task Partial_WithMarkupInTheMessage_EncodesIt()
    {
        var model = new FormErrorList("encoded-errors", ["<b>bold</b>"]);

        using var document = await PartialRenderer.RenderAsync(factory.Services, PartialPath, model);

        Assert.Empty(document.QuerySelectorAll("li b"));
        Assert.Equal("<b>bold</b>", document.QuerySelector("li")!.TextContent);
    }

    [Fact]
    public void For_WithShowEnabled_ReturnsTheFieldAndFormLevelErrorsWithoutDuplicates()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Name", "Name problem");
        modelState.AddModelError("Name", "Name problem");
        modelState.AddModelError("Other", "Other problem");
        modelState.AddModelError(string.Empty, "Form problem");

        var errors = FormErrors.For(modelState, show: true, "Name");

        Assert.Equal(["Name problem", "Form problem"], errors);
    }

    [Fact]
    public void For_WithShowDisabled_ReturnsNothing()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Name", "Name problem");

        Assert.Empty(FormErrors.For(modelState, show: false, "Name"));
        Assert.Empty(FormErrors.ForFieldsOnly(modelState, show: false, "Name"));
    }

    [Fact]
    public void ForFieldsOnly_LeavesFormLevelErrorsOut()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("email", "Email problem");
        modelState.AddModelError(string.Empty, "Form problem");

        Assert.Equal(["Email problem"], FormErrors.ForFieldsOnly(modelState, show: true, "email"));
    }

    [Fact]
    public void HasErrors_ReflectsOnlyTheNamedFieldAndTheShowFlag()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Name", "Name problem");
        modelState.AddModelError(string.Empty, "Form problem");

        Assert.True(FormErrors.HasErrors(modelState, show: true, "Name"));
        Assert.False(FormErrors.HasErrors(modelState, show: true, "Title"));
        Assert.False(FormErrors.HasErrors(modelState, show: false, "Name"));
    }
}
