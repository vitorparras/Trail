using Trail.Domain.Common;

namespace Trail.Domain.Tests.Common;

public sealed class NotificationTests
{
    private static readonly Message TotalNotPositive = Message.Error("QuestionCount.TotalNotPositive", "Total");
    private static readonly Message Exceeded = Message.Error("QuestionCount.Exceeded", "Correct");
    private static readonly Message ManyBlanks = Message.Warning("QuestionCount.ManyBlanks", "Total");

    [Fact]
    public void AddErrorIf_SeveralViolatedRules_AccumulatesEveryError()
    {
        var notification = Validate(total: 0, correct: 3, wrong: 2);

        Assert.True(notification.HasErrors);
        Assert.Equal([TotalNotPositive, Exceeded], notification.Messages);
    }

    [Fact]
    public void AddErrorIf_ConditionIsFalse_AddsNothing()
    {
        var notification = Validate(total: 10, correct: 3, wrong: 2);

        Assert.False(notification.HasErrors);
        Assert.Empty(notification.Messages);
    }

    [Fact]
    public void Add_MessagesFromAnotherSource_AppendsThemInOrder()
    {
        Result invalid = Result.Invalid(TotalNotPositive, Exceeded);

        var notification = new Notification().Add(ManyBlanks).Add(invalid.Messages);

        Assert.Equal([ManyBlanks, TotalNotPositive, Exceeded], notification.Messages);
    }

    [Fact]
    public void Add_NullMessages_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Notification().Add(messages: null!));
    }

    [Fact]
    public void HasErrors_OnlyNonErrorMessages_IsFalse()
    {
        var notification = new Notification().Add(ManyBlanks, Message.Info("QuestionCount.Hint"));

        Assert.False(notification.HasErrors);
    }

    [Fact]
    public void Messages_CastToMutableList_CannotBeChanged()
    {
        var notification = new Notification().Add(ManyBlanks);

        var messages = Assert.IsAssignableFrom<IList<Message>>(notification.Messages);

        Assert.Throws<NotSupportedException>(() => messages.Add(Exceeded));
    }

    [Fact]
    public void ToResult_WithErrors_ReturnsInvalidWithEveryMessage()
    {
        var notification = Validate(total: 0, correct: 3, wrong: 2).Add(ManyBlanks);

        var result = notification.ToResult();

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal([TotalNotPositive, Exceeded, ManyBlanks], result.Messages);
    }

    [Fact]
    public void ToResult_WithoutErrors_ReturnsOkKeepingWarnings()
    {
        var notification = Validate(total: 10, correct: 3, wrong: 2).Add(ManyBlanks);

        var result = notification.ToResult();

        Assert.True(result.IsOk);
        Assert.Equal([ManyBlanks], result.Messages);
    }

    [Fact]
    public void ToResult_NotificationChangedAfterwards_KeepsTheMessagesItWasCreatedWith()
    {
        var notification = new Notification().Add(ManyBlanks);

        var result = notification.ToResult();
        notification.Add(Exceeded);

        Assert.Equal([ManyBlanks], result.Messages);
    }

    [Fact]
    public void ToResultOfT_WithErrors_ReturnsInvalidWithoutCreatingData()
    {
        var notification = Validate(total: 0, correct: 3, wrong: 2);
        var created = false;

        var result = notification.ToResult(() =>
        {
            created = true;
            return 42;
        });

        Assert.False(created);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal([TotalNotPositive, Exceeded], result.Messages);
    }

    [Fact]
    public void ToResultOfT_WithoutErrors_ReturnsOkWithCreatedDataAndMessages()
    {
        var notification = Validate(total: 10, correct: 3, wrong: 2).Add(ManyBlanks);

        var result = notification.ToResult(() => 42);

        Assert.Equal(42, result.Data);
        Assert.Equal([ManyBlanks], result.Messages);
    }

    [Fact]
    public void ToResultOfT_WithNullFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Notification().ToResult<int>(null!));
    }

    private static Notification Validate(int total, int correct, int wrong) =>
        new Notification()
            .AddErrorIf(total <= 0, "QuestionCount.TotalNotPositive", "Total")
            .AddErrorIf(correct + wrong > total, "QuestionCount.Exceeded", "Correct");
}
