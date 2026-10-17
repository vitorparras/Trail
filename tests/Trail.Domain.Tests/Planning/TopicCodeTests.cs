using System.Reflection;
using CsCheck;
using Trail.Domain.Planning;

namespace Trail.Domain.Tests.Planning;

public sealed class TopicCodeTests
{
    [Fact]
    public void Create_WithValidCode_NormalizesAndParsesSegments()
    {
        var result = TopicCode.Create("04.01.");

        Assert.True(result.IsOk);
        Assert.Equal("4.1", result.Data.Value);
        Assert.Equal([4, 1], result.Data.Segments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("4..1")]
    [InlineData("4.1.a")]
    [InlineData("-1.2")]
    public void Create_WithInvalidCode_ReturnsInvalid(string? rawCode)
    {
        var result = TopicCode.Create(rawCode);

        Assert.False(result.IsOk);
        Assert.Equal("planning.topic.invalid_code", result.Failure.Messages[0].Code);
    }

    [Fact]
    public void ParentCode_WithSingleSegment_ReturnsNull()
    {
        var code = TopicCode.Create("5").Data;

        Assert.Null(code.ParentCode);
    }

    [Fact]
    public void ParentCode_WithMultipleSegments_ReturnsParent()
    {
        var code = TopicCode.Create("4.1.2").Data;

        Assert.NotNull(code.ParentCode);
        Assert.Equal("4.1", code.ParentCode.Value);
        Assert.Equal([4, 1], code.ParentCode.Segments);
    }

    [Fact]
    public void CompareTo_NaturalNumericOrdering_OrdersCorrectly()
    {
        var code49 = TopicCode.Create("4.9").Data;
        var code410 = TopicCode.Create("4.10").Data;
        var code41 = TopicCode.Create("4.1").Data;
        var code411 = TopicCode.Create("4.1.1").Data;

        Assert.True(code49 < code410);
        Assert.True(code41 < code411);
        Assert.True(code410 > code49);
        Assert.True(code410 >= code49);
        Assert.True(code49 <= code410);
        Assert.Equal(0, code49.CompareTo(TopicCode.Create("4.9").Data));
    }

    [Fact]
    public void ParseOutlineLine_WithTrailingDot_ParsesCodeAndTitle()
    {
        var (code, title, hadInvalidPrefix) = TopicCode.ParseOutlineLine("4.1. Título");

        Assert.NotNull(code);
        Assert.Equal("4.1", code.Value);
        Assert.Equal("Título", title);
        Assert.False(hadInvalidPrefix);
    }

    [Fact]
    public void ParseOutlineLine_WithoutTrailingDot_ParsesCodeAndTitle()
    {
        var (code, title, hadInvalidPrefix) = TopicCode.ParseOutlineLine("4.1 Título");

        Assert.NotNull(code);
        Assert.Equal("4.1", code.Value);
        Assert.Equal("Título", title);
        Assert.False(hadInvalidPrefix);
    }

    [Fact]
    public void ParseOutlineLine_WithLeadingSpaces_ParsesCodeAndTitle()
    {
        var (code, title, hadInvalidPrefix) = TopicCode.ParseOutlineLine("  12.3.4. Subtópico");

        Assert.NotNull(code);
        Assert.Equal("12.3.4", code.Value);
        Assert.Equal("Subtópico", title);
        Assert.False(hadInvalidPrefix);
    }

    [Fact]
    public void ParseOutlineLine_WithUnnumberedLine_ReturnsNullCodeAndOriginalTitle()
    {
        var (code, title, hadInvalidPrefix) = TopicCode.ParseOutlineLine("ENGENHARIA REVERSA DE SOFTWARE");

        Assert.Null(code);
        Assert.Equal("ENGENHARIA REVERSA DE SOFTWARE", title);
        Assert.False(hadInvalidPrefix);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseOutlineLine_WithNullOrWhitespace_ReturnsNullCodeAndEmptyTitle(string? input)
    {
        var (code, title, hadInvalidPrefix) = TopicCode.ParseOutlineLine(input!);

        Assert.Null(code);
        Assert.Equal(string.Empty, title);
        Assert.False(hadInvalidPrefix);
    }

    [Fact]
    public void ParseOutlineLine_WithInvalidPrefix_FlagsHadInvalidNumberPrefix()
    {
        var (code, title, hadInvalidPrefix) = TopicCode.ParseOutlineLine("4.1.");

        Assert.Null(code);
        Assert.True(hadInvalidPrefix);
    }

    [Fact]
    public void EqualityAndToString_WorkAsExpected()
    {
        var code1 = TopicCode.Create("4.1").Data;
        var code2 = TopicCode.Create("04.1.").Data;
        var code3 = TopicCode.Create("4.2").Data;

        Assert.True(code1 == code2);
        Assert.False(code1 != code2);
        Assert.True(code1 != code3);
        Assert.Equal(code1, code2);
        Assert.Equal(code1.GetHashCode(), code2.GetHashCode());
        Assert.Equal("4.1", code1.ToString());
        Assert.False(code1.Equals(null));
        Assert.False(code1.Equals(new object()));
    }

    [Fact]
    public void Property_RoundTripFormattingAndParentDerivation()
    {
        Gen.Int[0, 999].Array[1, 6].Sample(segments =>
        {
            var raw = string.Join('.', segments);
            var result = TopicCode.Create(raw);

            Assert.True(result.IsOk);
            var code = result.Data;

            Assert.Equal(raw, code.Value);
            Assert.Equal(segments, code.Segments);

            if (segments.Length == 1)
            {
                Assert.Null(code.ParentCode);
            }
            else
            {
                Assert.NotNull(code.ParentCode);
                Assert.Equal(segments.Take(segments.Length - 1), code.ParentCode.Segments);
                Assert.Equal(string.Join('.', segments.Take(segments.Length - 1)), code.ParentCode.Value);
            }
        });
    }

    [Fact]
    public void Property_OrderingSatisfiesTransitivityAndReflexivity()
    {
        var genCode = Gen.Int[0, 99].Array[1, 4].Select(s => TopicCode.Create(string.Join('.', s)).Data);

        Gen.Select(genCode, genCode, genCode).Sample((a, b, c) =>
        {
            var sameA = TopicCode.Create(a.Value).Data;
            Assert.Equal(0, a.CompareTo(sameA));
            Assert.True(a <= sameA);
            Assert.True(a >= sameA);

            var ab = a.CompareTo(b);
            var ba = b.CompareTo(a);
            Assert.Equal(Math.Sign(ab), -Math.Sign(ba));

            var bc = b.CompareTo(c);
            if (ab > 0 && bc > 0)
            {
                Assert.True(a.CompareTo(c) > 0);
            }
            else if (ab < 0 && bc < 0)
            {
                Assert.True(a.CompareTo(c) < 0);
            }
        });
    }

    [Fact]
    public void ParameterlessConstructor_InitializesEmpty()
    {
        var constructor = typeof(TopicCode).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        Assert.NotNull(constructor);
        var code = (TopicCode)constructor.Invoke(null);
        Assert.Equal(string.Empty, code.Value);
        Assert.Empty(code.Segments);
    }

    [Fact]
    public void Create_WithOverflowingSegment_ReturnsInvalid()
    {
        var result = TopicCode.Create("1.99999999999999999999999");

        Assert.False(result.IsOk);
        Assert.Equal("planning.topic.invalid_code", result.Failure.Messages[0].Code);
    }

    [Fact]
    public void ParseOutlineLine_WithOverflowingCodeSegment_ReturnsInvalidPrefix()
    {
        var (code, title, hadInvalidPrefix) = TopicCode.ParseOutlineLine("99999999999999999999999 Título");

        Assert.Null(code);
        Assert.Equal("99999999999999999999999 Título", title);
        Assert.True(hadInvalidPrefix);
    }

    [Fact]
    public void ComparisonAndEquality_EdgeCases_HandleNullAndSelf()
    {
        var code = TopicCode.Create("4.1").Data;

        Assert.Equal(0, code.CompareTo(code));
        Assert.Equal(1, code.CompareTo(null));
        Assert.True(code.Equals((object)code));
        Assert.False(code.Equals((object?)null));

        Assert.True(null < code);
        Assert.False(code < null);
        Assert.True(code > null);
        Assert.False(null > code);
        Assert.True(null <= code);
        Assert.True(code >= null);
        Assert.False(code <= null);
        Assert.False(null >= code);
    }
}
