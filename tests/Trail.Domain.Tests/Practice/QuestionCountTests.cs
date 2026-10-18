using System.Reflection;
using CsCheck;
using Trail.Domain.Practice;

namespace Trail.Domain.Tests.Practice;

public sealed class QuestionCountTests
{
    private static readonly Gen<int> AnyInt = Gen.Int;

    [Fact]
    public void Create_WithValidValues_ReturnsTheCountWithDerivedBlank()
    {
        var result = QuestionCount.Create(10, 6, 3);

        Assert.True(result.IsOk);
        Assert.Equal(10, result.Data.Total);
        Assert.Equal(6, result.Data.Correct);
        Assert.Equal(3, result.Data.Wrong);
        Assert.Equal(1, result.Data.Blank);
    }

    [Theory]
    [InlineData(1, 0, 0, 1)]
    [InlineData(1, 1, 0, 0)]
    [InlineData(1, 0, 1, 0)]
    [InlineData(5, 3, 2, 0)]
    [InlineData(int.MaxValue, int.MaxValue, 0, 0)]
    [InlineData(int.MaxValue, 0, int.MaxValue, 0)]
    public void Create_AtTheBoundaries_IsAccepted(int total, int correct, int wrong, int blank)
    {
        var result = QuestionCount.Create(total, correct, wrong);

        Assert.True(result.IsOk);
        Assert.Equal(blank, result.Data.Blank);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_WithNonPositiveTotal_ReturnsInvalid(int total)
    {
        var result = QuestionCount.Create(total, 0, 0);

        Assert.False(result.IsOk);
        var message = Assert.Single(result.Failure.Messages);
        Assert.Equal("practice.question_count.total_must_be_positive", message.Code);
        Assert.Equal(nameof(QuestionCount.Total), message.Target);
    }

    [Fact]
    public void Create_WithNegativeCorrect_ReturnsInvalid()
    {
        var result = QuestionCount.Create(10, -1, 0);

        Assert.False(result.IsOk);
        var message = Assert.Single(result.Failure.Messages);
        Assert.Equal("practice.question_count.correct_must_not_be_negative", message.Code);
        Assert.Equal(nameof(QuestionCount.Correct), message.Target);
    }

    [Fact]
    public void Create_WithNegativeWrong_ReturnsInvalid()
    {
        var result = QuestionCount.Create(10, 0, -1);

        Assert.False(result.IsOk);
        var message = Assert.Single(result.Failure.Messages);
        Assert.Equal("practice.question_count.wrong_must_not_be_negative", message.Code);
        Assert.Equal(nameof(QuestionCount.Wrong), message.Target);
    }

    [Theory]
    [InlineData(10, 6, 5)]
    [InlineData(1, 1, 1)]
    [InlineData(10, 11, 0)]
    [InlineData(10, 0, 11)]
    [InlineData(int.MaxValue, int.MaxValue, 1)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue)]
    public void Create_WhenCorrectPlusWrongExceedsTotal_ReturnsInvalid(int total, int correct, int wrong)
    {
        var result = QuestionCount.Create(total, correct, wrong);

        Assert.False(result.IsOk);
        var message = Assert.Single(result.Failure.Messages);
        Assert.Equal("practice.question_count.answers_exceed_total", message.Code);
        Assert.Equal(nameof(QuestionCount.Correct), message.Target);
    }

    [Fact]
    public void Create_WithSeveralViolations_AccumulatesEveryIndividualError()
    {
        var result = QuestionCount.Create(0, -1, -2);

        Assert.False(result.IsOk);
        Assert.Equal(
            [
                "practice.question_count.total_must_be_positive",
                "practice.question_count.correct_must_not_be_negative",
                "practice.question_count.wrong_must_not_be_negative",
            ],
            result.Failure.Messages.Select(message => message.Code));
    }

    [Fact]
    public void Create_WithNegativeValueAndLargeOther_DoesNotReportTheSumError()
    {
        var result = QuestionCount.Create(5, -3, 10);

        Assert.False(result.IsOk);
        Assert.Equal(
            ["practice.question_count.correct_must_not_be_negative"],
            result.Failure.Messages.Select(message => message.Code));
    }

    [Fact]
    public void Create_AcceptsExactlyTheValuesSatisfyingTheInvariants()
    {
        AnyInt.Select(AnyInt, AnyInt).Sample((total, correct, wrong) =>
        {
            var satisfies = total > 0 && correct >= 0 && wrong >= 0 && (long)correct + wrong <= total;

            Assert.Equal(satisfies, QuestionCount.Create(total, correct, wrong).IsOk);
        });
    }

    [Fact]
    public void Create_WithValuesInTypicalRange_AcceptsExactlyTheValuesSatisfyingTheInvariants()
    {
        var small = Gen.Int[-5, 60];

        small.Select(small, small).Sample((total, correct, wrong) =>
        {
            var satisfies = total > 0 && correct >= 0 && wrong >= 0 && correct + wrong <= total;

            Assert.Equal(satisfies, QuestionCount.Create(total, correct, wrong).IsOk);
        });
    }

    [Fact]
    public void Create_WhenValid_PreservesTheComponentsAndBlankCompletesTheTotal()
    {
        Gen.Int[1, 100_000].SelectMany(total =>
            Gen.Int[0, total].SelectMany(correct =>
                Gen.Int[0, total - correct].Select(wrong => (total, correct, wrong))))
            .Sample(values =>
            {
                var result = QuestionCount.Create(values.total, values.correct, values.wrong);

                Assert.True(result.IsOk);
                Assert.Equal(values.total, result.Data.Total);
                Assert.Equal(values.correct, result.Data.Correct);
                Assert.Equal(values.wrong, result.Data.Wrong);
                Assert.True(result.Data.Blank >= 0);
                Assert.Equal(values.total, result.Data.Blank + result.Data.Correct + result.Data.Wrong);
            });
    }

    [Fact]
    public void Equals_WithSameComponents_IsEqualWithSameHashCode()
    {
        var left = QuestionCount.Create(10, 6, 3).Data;
        var right = QuestionCount.Create(10, 6, 3).Data;

        Assert.True(left.Equals(right));
        Assert.True(left.Equals((object)right));
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Theory]
    [InlineData(11, 6, 3)]
    [InlineData(10, 5, 3)]
    [InlineData(10, 6, 2)]
    public void Equals_WithDifferentComponents_IsNotEqual(int total, int correct, int wrong)
    {
        var left = QuestionCount.Create(10, 6, 3).Data;
        var right = QuestionCount.Create(total, correct, wrong).Data;

        Assert.False(left.Equals(right));
        Assert.False(left == right);
        Assert.True(left != right);
    }

    [Fact]
    public void Equals_WithNullOrOtherType_IsNotEqual()
    {
        var count = QuestionCount.Create(10, 6, 3).Data;

        Assert.False(count.Equals(null));
        Assert.False(count.Equals((object?)null));
        Assert.False(count.Equals("10"));
        Assert.False(count == null);
        Assert.True(count != null);
        Assert.False(null == count);
        Assert.True(((QuestionCount?)null) == null);
    }

    [Fact]
    public void ParameterlessConstructor_InitializesAllComponentsToZero()
    {
        var constructor = typeof(QuestionCount).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        Assert.NotNull(constructor);
        var count = (QuestionCount)constructor.Invoke(null);
        Assert.Equal(0, count.Total);
        Assert.Equal(0, count.Correct);
        Assert.Equal(0, count.Wrong);
        Assert.Equal(0, count.Blank);
    }
}
