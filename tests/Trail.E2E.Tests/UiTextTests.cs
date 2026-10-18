namespace Trail.E2E.Tests;

public sealed class UiTextTests
{
    private const string SharedOnlyKey = "SkipToContent";

    [Theory]
    [InlineData("practice.sample.missing", "PracticeResource.resx")]
    [InlineData("fitness.sample.missing", "FitnessResource.resx")]
    [InlineData("studytime.sample.missing", "StudyTimeResource.resx")]
    [InlineData("pwa.sample.missing", "PwaResource.resx")]
    [InlineData("planning.sample.missing", "SharedResource.resx")]
    public void Get_MessageCode_ReadsTheResourceOfItsPrefix(string code, string fileName)
    {
        var exception = Assert.Throws<KeyNotFoundException>(() => UiText.Get(code));

        Assert.EndsWith($" {fileName}.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Get_KeyOfModule_ReadsOnlyTheResourceOfThatModule()
    {
        Assert.Equal(UiText.Get(SharedOnlyKey), UiText.Get(UiResource.Shared, SharedOnlyKey));

        foreach (var resource in Enum.GetValues<UiResource>().Where(resource => resource is not UiResource.Shared))
        {
            var exception = Assert.Throws<KeyNotFoundException>(() => UiText.Get(resource, SharedOnlyKey));

            Assert.EndsWith($" {resource}Resource.resx.", exception.Message, StringComparison.Ordinal);
        }
    }
}
