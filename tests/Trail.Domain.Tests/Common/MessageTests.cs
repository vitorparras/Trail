using Trail.Domain.Common;

namespace Trail.Domain.Tests.Common;

public sealed class MessageTests
{
    [Theory]
    [InlineData(MessageSeverity.Error)]
    [InlineData(MessageSeverity.Warning)]
    [InlineData(MessageSeverity.Info)]
    [InlineData(MessageSeverity.Success)]
    public void Factory_ForEachSeverity_CreatesMessageWithThatSeverity(MessageSeverity severity)
    {
        var message = severity switch
        {
            MessageSeverity.Error => Message.Error("QuestionCount.Exceeded", "Correct"),
            MessageSeverity.Warning => Message.Warning("QuestionCount.Exceeded", "Correct"),
            MessageSeverity.Info => Message.Info("QuestionCount.Exceeded", "Correct"),
            _ => Message.Success("QuestionCount.Exceeded", "Correct"),
        };

        Assert.Equal(new Message("QuestionCount.Exceeded", severity, "Correct"), message);
    }

    [Fact]
    public void Constructor_WithoutTarget_LeavesTargetEmpty()
    {
        var message = new Message("StudyPlan.Saved", MessageSeverity.Success);

        Assert.Equal("StudyPlan.Saved", message.Code);
        Assert.Equal(MessageSeverity.Success, message.Severity);
        Assert.Null(message.Target);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankCode_ThrowsArgumentException(string? code)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new Message(code!, MessageSeverity.Error));

        Assert.Equal("code", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankTarget_ThrowsArgumentException(string target)
    {
        var exception = Assert.Throws<ArgumentException>(() => new Message("QuestionCount.Exceeded", MessageSeverity.Error, target));

        Assert.Equal("target", exception.ParamName);
    }

    [Fact]
    public void Equals_SameCodeSeverityAndTarget_IsTrue()
    {
        Assert.Equal(Message.Error("Topic.CodeTaken", "Code"), Message.Error("Topic.CodeTaken", "Code"));
        Assert.NotEqual(Message.Error("Topic.CodeTaken", "Code"), Message.Warning("Topic.CodeTaken", "Code"));
    }
}
