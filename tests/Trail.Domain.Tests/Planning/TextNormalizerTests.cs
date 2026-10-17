using System.Globalization;
using CsCheck;
using Trail.Domain.Planning;

namespace Trail.Domain.Tests.Planning;

public sealed class TextNormalizerTests
{
    [Fact]
    public void NormalizeSearchText_WithPortugueseAccents_StripsAccentsAndConvertsToLowercase()
    {
        var result = TextNormalizer.NormalizeSearchText("Árvore de Decisão e Programação Dinâmica");

        Assert.Equal("arvore de decisao e programacao dinamica", result);
    }

    [Fact]
    public void NormalizeSearchText_WithCodeAndTitle_JoinsAndCollapsesWhitespace()
    {
        var result = TextNormalizer.NormalizeSearchText("4.1", "  Árvore   de   Decisão  ");

        Assert.Equal("4.1 arvore de decisao", result);
    }

    [Fact]
    public void NormalizeSearchText_WithEmptyOrNullParts_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TextNormalizer.NormalizeSearchText());
        Assert.Equal(string.Empty, TextNormalizer.NormalizeSearchText(null, "", "   "));
    }

    [Fact]
    public void Property_Idempotent()
    {
        Gen.String.Sample(input =>
        {
            var first = TextNormalizer.NormalizeSearchText(input);
            var second = TextNormalizer.NormalizeSearchText(first);

            Assert.Equal(first, second);
        });
    }

    [Fact]
    public void Property_ContainsNoNonSpacingMarksOrUppercase()
    {
        Gen.String.Sample(input =>
        {
            var normalized = TextNormalizer.NormalizeSearchText(input);

            Assert.True(normalized.All(c => !char.IsUpper(c)));
            Assert.True(normalized.All(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
        });
    }

    [Fact]
    public void NormalizeSearchText_WithArray_CallsSpanOverload()
    {
        string?[] parts = ["4.1", "  Árvore  "];
        var result = TextNormalizer.NormalizeSearchText(parts);

        Assert.Equal("4.1 arvore", result);
    }
}
