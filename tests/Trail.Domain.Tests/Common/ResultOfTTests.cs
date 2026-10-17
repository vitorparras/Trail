using Trail.Domain.Common;

namespace Trail.Domain.Tests.Common;

public sealed class ResultOfTTests
{
    private static readonly Message Recorded = Message.Success("QuestionEntry.Recorded");
    private static readonly Message Notice = Message.Warning("QuestionEntry.ManyBlanks", "Blank");

    [Fact]
    public void Ok_WithData_ExposesDataWithOkStatus()
    {
        var result = Result.Ok(42);

        Assert.True(result.IsOk);
        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(42, result.Data);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void Ok_WithDataAndMessages_KeepsMessagesInOrder()
    {
        var result = Result.Ok("view", Recorded, Notice);

        Assert.Equal("view", result.Data);
        Assert.Equal([Recorded, Notice], result.Messages);
    }

    [Fact]
    public void Ok_WithDataAndErrorMessage_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Result.Ok(42, Failures.Error));
    }

    [Fact]
    public void Ok_WithDataAndNullMessages_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Ok(42, messages: null!));
    }

    [Theory]
    [InlineData(ResultStatus.Invalid)]
    [InlineData(ResultStatus.NotFound)]
    [InlineData(ResultStatus.Conflict)]
    [InlineData(ResultStatus.Forbidden)]
    public void ImplicitConversion_FromFailure_KeepsStatusAndMessages(ResultStatus status)
    {
        Result<int> result = Failures.Create(status);

        Assert.False(result.IsOk);
        Assert.Equal(status, result.Status);
        Assert.Equal([Failures.Error], result.Messages);
    }

    [Theory]
    [InlineData(ResultStatus.Invalid)]
    [InlineData(ResultStatus.NotFound)]
    [InlineData(ResultStatus.Conflict)]
    [InlineData(ResultStatus.Forbidden)]
    public void Data_WhenNotOk_ThrowsInvalidOperationException(ResultStatus status)
    {
        Result<int> result = Failures.Create(status);

        var exception = Assert.Throws<InvalidOperationException>(() => result.Data);

        Assert.Contains(status.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImplicitConversion_FromNullFailure_ThrowsArgumentNullException()
    {
        Failure failure = null!;

        Assert.Throws<ArgumentNullException>(() => (Result<int>)failure);
    }

    [Fact]
    public void Failure_WhenOk_ThrowsInvalidOperationException()
    {
        var result = Result.Ok(42);

        Assert.Throws<InvalidOperationException>(() => result.Failure);
    }

    [Fact]
    public void Failure_WhenNotOk_CanBeReturnedAsResultOfAnotherType()
    {
        Result<int> source = Result.NotFound(Failures.Error);

        Result<string> propagated = source.Failure;
        Result withoutData = source.Failure;

        Assert.Equal(ResultStatus.NotFound, propagated.Status);
        Assert.Equal(ResultStatus.NotFound, withoutData.Status);
        Assert.Equal([Failures.Error], propagated.Messages);
    }

    [Fact]
    public void Map_WhenOk_TransformsDataAndKeepsMessages()
    {
        var result = Result.Ok(21, Recorded);

        var mapped = result.Map(value => value * 2);

        Assert.Equal(42, mapped.Data);
        Assert.Equal([Recorded], mapped.Messages);
    }

    [Fact]
    public void Map_WhenNotOk_PropagatesFailureWithoutInvokingMap()
    {
        Result<int> result = Result.Conflict(Failures.Error);
        var invoked = false;

        var mapped = result.Map(value =>
        {
            invoked = true;
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        });

        Assert.False(invoked);
        Assert.Equal(ResultStatus.Conflict, mapped.Status);
        Assert.Equal([Failures.Error], mapped.Messages);
    }

    [Fact]
    public void Map_WithNullDelegate_ThrowsArgumentNullException()
    {
        var result = Result.Ok(42);

        Assert.Throws<ArgumentNullException>(() => result.Map<int>(null!));
    }

    [Fact]
    public void Match_WhenOk_InvokesOnOkWithData()
    {
        var result = Result.Ok(42);

        var outcome = result.Match(value => value + 1, failure => -1);

        Assert.Equal(43, outcome);
    }

    [Fact]
    public void Match_WhenNotOk_InvokesOnFailureWithTheFailure()
    {
        Result<int> result = Result.Forbidden();

        var outcome = result.Match(value => ResultStatus.Ok, failure => failure.Status);

        Assert.Equal(ResultStatus.Forbidden, outcome);
    }

    [Fact]
    public void Match_WithNullDelegate_ThrowsArgumentNullException()
    {
        var result = Result.Ok(42);

        Assert.Throws<ArgumentNullException>(() => result.Match(null!, failure => 0));
        Assert.Throws<ArgumentNullException>(() => result.Match(value => value, null!));
    }
}
