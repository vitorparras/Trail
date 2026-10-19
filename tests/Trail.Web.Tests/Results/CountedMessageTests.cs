using System.Globalization;
using Microsoft.Extensions.Localization;
using Trail.Web.Results;

namespace Trail.Web.Tests.Results;

public sealed class CountedMessageTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    [Theory]
    [InlineData(0, "planning.deletion.notes.other")]
    [InlineData(1, "planning.deletion.notes.one")]
    [InlineData(2, "planning.deletion.notes.other")]
    [InlineData(1_000, "planning.deletion.notes.other")]
    public void KeyFor_UsesTheSingularOnlyForExactlyOne(int count, string expected)
    {
        Assert.Equal(expected, CountedMessage.KeyFor("planning.deletion.notes", count));
    }

    [Fact]
    public void KeysFor_ReturnsTheSingularAndThePluralKeys()
    {
        Assert.Equal(["practice.deletion.question_entries.one", "practice.deletion.question_entries.other"], CountedMessage.KeysFor("practice.deletion.question_entries"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(1_234)]
    public void LocalizeCount_FormatsTheFormOfTheCountWithThePortugueseNumberFormat(int count)
    {
        var previous = CultureInfo.CurrentCulture;
        var portuguese = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.CurrentCulture = portuguese;
        try
        {
            var localizerFactory = factory.Services.GetRequiredService<IStringLocalizerFactory>();
            var form = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>()[count == 1 ? "planning.deletion.notes.one" : "planning.deletion.notes.other"];

            var text = localizerFactory.LocalizeCount("planning.deletion.notes", count);

            Assert.False(text.ResourceNotFound);
            Assert.Equal(string.Format(portuguese, form.Value, count), text.Value);
            Assert.StartsWith(count.ToString("N0", portuguese) + " ", text.Value, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void LocalizeCount_WithoutTheForms_ReportsTheMissingKey()
    {
        var text = factory.Services.GetRequiredService<IStringLocalizerFactory>().LocalizeCount("practice.sample.missing", 2);

        Assert.True(text.ResourceNotFound);
        Assert.Equal("practice.sample.missing.other", text.Name);
    }
}
