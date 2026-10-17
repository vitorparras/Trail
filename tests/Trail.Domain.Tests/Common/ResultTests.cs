using Trail.Domain.Common;

namespace Trail.Domain.Tests.Common;

public sealed class ResultTests
{
    private static readonly Message Saved = Message.Success("StudyPlan.Saved");
    private static readonly Message Notice = Message.Warning("StudyPlan.EmptyBlock", "Blocks");
    private static readonly Message Hint = Message.Info("StudyPlan.ImportAvailable");

    [Fact]
    public void Ok_WithoutArguments_ResolvesToResultWithoutMessages()
    {
        var result = Result.Ok();

        Assert.Equal(typeof(Result), StaticTypeOf(result));
        Assert.True(result.IsOk);
        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void Ok_WithOneMessage_ResolvesToResultCarryingTheMessage()
    {
        var result = Result.Ok(Saved);

        Assert.Equal(typeof(Result), StaticTypeOf(result));
        Assert.Equal([Saved], result.Messages);
    }

    [Fact]
    public void Ok_WithSeveralMessages_ResolvesToResultKeepingTheirOrder()
    {
        var result = Result.Ok(Saved, Notice, Hint);

        Assert.Equal(typeof(Result), StaticTypeOf(result));
        Assert.Equal([Saved, Notice, Hint], result.Messages);
    }

    [Fact]
    public void Ok_WithReadOnlyMessageList_ResolvesToResultCarryingTheMessages()
    {
        var source = Result.Ok(Saved, Notice);

        var result = Result.Ok(source.Messages);

        Assert.Equal(typeof(Result), StaticTypeOf(result));
        Assert.Equal([Saved, Notice], result.Messages);
    }

    [Fact]
    public void Ok_WithDataAndMessages_ResolvesToResultOfTheData()
    {
        var result = Result.Ok(42, Saved);

        Assert.Equal(typeof(Result<int>), StaticTypeOf(result));
    }

    [Fact]
    public void Ok_WithMutableMessageList_ResolvesToResultOfTheList()
    {
        List<Message> messages = [Saved];

        var result = Result.Ok(messages);

        Assert.Equal(typeof(Result<List<Message>>), StaticTypeOf(result));
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void Ok_WithMessageCollection_CopiesTheCollection()
    {
        List<Message> source = [Saved];

        var result = Result.Ok(messages: source);
        source.Add(Notice);

        Assert.Equal([Saved], result.Messages);
    }

    [Fact]
    public void Ok_WithErrorMessage_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => Result.Ok(Saved, Failures.Error));

        Assert.Equal("messages", exception.ParamName);
    }

    [Fact]
    public void Ok_WithNullMessages_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Ok(messages: (IEnumerable<Message>)null!));
        Assert.Throws<ArgumentNullException>(() => Result.Ok(messages: (IReadOnlyList<Message>)null!));
    }

    [Fact]
    public void Messages_CastToMutableList_CannotBeChanged()
    {
        var result = Result.Ok(Saved);

        var messages = Assert.IsAssignableFrom<IList<Message>>(result.Messages);

        Assert.Throws<NotSupportedException>(() => messages.Add(Notice));
    }

    [Fact]
    public void Invalid_WithErrorsAndWarnings_KeepsEveryMessage()
    {
        var otherError = Message.Error("Sample.Missing", "OtherField");

        Result result = Result.Invalid(Failures.Error, Notice, otherError);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal([Failures.Error, Notice, otherError], result.Messages);
    }

    [Fact]
    public void Invalid_WithMessageCollection_CopiesTheCollection()
    {
        List<Message> source = [Failures.Error];

        var failure = Result.Invalid(source);
        source.Add(Notice);

        Assert.Equal([Failures.Error], failure.Messages);
    }

    [Fact]
    public void Invalid_WithoutMessages_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Result.Invalid());
    }

    [Fact]
    public void Invalid_WithoutErrorMessage_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => Result.Invalid(Notice, Hint));

        Assert.Equal("messages", exception.ParamName);
    }

    [Theory]
    [InlineData(ResultStatus.NotFound)]
    [InlineData(ResultStatus.Conflict)]
    [InlineData(ResultStatus.Forbidden)]
    public void FailureFactory_WithoutMessages_ReturnsStatusWithoutMessages(ResultStatus status)
    {
        Result result = status switch
        {
            ResultStatus.NotFound => Result.NotFound(),
            ResultStatus.Conflict => Result.Conflict(),
            _ => Result.Forbidden(),
        };

        Assert.False(result.IsOk);
        Assert.Equal(status, result.Status);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void ConcurrencyConflict_Called_ReturnsConflictWithTheStableCode()
    {
        Result result = Result.ConcurrencyConflict();

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal([Message.Warning("Concurrency.Conflict")], result.Messages);
        Assert.Equal("Concurrency.Conflict", Result.ConcurrencyConflictCode);
    }

    [Theory]
    [InlineData(ResultStatus.Invalid)]
    [InlineData(ResultStatus.NotFound)]
    [InlineData(ResultStatus.Conflict)]
    [InlineData(ResultStatus.Forbidden)]
    public void ImplicitConversion_FromFailure_KeepsStatusAndMessages(ResultStatus status)
    {
        var failure = Failures.Create(status);

        Result result = failure;

        Assert.Equal(status, result.Status);
        Assert.Equal([Failures.Error], result.Messages);
        Assert.Same(failure, result.Failure);
    }

    [Fact]
    public void ImplicitConversion_FromNullFailure_ThrowsArgumentNullException()
    {
        Failure failure = null!;

        Assert.Throws<ArgumentNullException>(() => (Result)failure);
    }

    [Fact]
    public void Failure_WhenOk_ThrowsInvalidOperationException()
    {
        var result = Result.Ok();

        Assert.Throws<InvalidOperationException>(() => result.Failure);
    }

    [Fact]
    public void IResultEnvelope_ImplementedByEveryShape_ExposesStatusAndMessages()
    {
        var failure = Result.NotFound(Failures.Error);
        Result result = failure;
        Result<int> resultOfT = failure;

        IResultEnvelope[] envelopes = [failure, result, resultOfT, Result.Ok(Saved), Result.Ok(42, Saved)];

        Assert.Equal(
            [ResultStatus.NotFound, ResultStatus.NotFound, ResultStatus.NotFound, ResultStatus.Ok, ResultStatus.Ok],
            envelopes.Select(envelope => envelope.Status));
        Assert.Equal(
            [Failures.Error, Failures.Error, Failures.Error, Saved, Saved],
            envelopes.Select(envelope => Assert.Single(envelope.Messages)));
    }

    [Fact]
    public void Match_WhenOk_InvokesOnOk()
    {
        var result = Result.Ok();

        var outcome = result.Match(() => "ok", failure => failure.Status.ToString());

        Assert.Equal("ok", outcome);
    }

    [Fact]
    public void Match_WhenNotOk_InvokesOnFailureWithTheFailure()
    {
        Result result = Result.Conflict(Failures.Error);

        var outcome = result.Match(() => ResultStatus.Ok, failure => failure.Status);

        Assert.Equal(ResultStatus.Conflict, outcome);
    }

    [Fact]
    public void Match_WithNullDelegate_ThrowsArgumentNullException()
    {
        var result = Result.Ok();

        Assert.Throws<ArgumentNullException>(() => result.Match(null!, failure => 0));
        Assert.Throws<ArgumentNullException>(() => result.Match(() => 0, null!));
    }

    private static Type StaticTypeOf<T>(T value) => typeof(T);
}
