using System.Reflection;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Domain.Tests.Planning;

public sealed class TopicNoteTests
{
    private readonly UserId _ownerId = UserId.New();
    private readonly TopicId _topicId = TopicId.New();

    [Fact]
    public void Create_WithValidMarkdown_ReturnsNote()
    {
        var markdown = "# Anotações sobre o tema\n\n- Ponto 1\n- Ponto 2";

        var result = TopicNote.Create(_ownerId, _topicId, markdown);

        Assert.True(result.IsOk);
        var note = result.Data;
        Assert.Equal(_ownerId, note.OwnerId);
        Assert.Equal(_topicId, note.TopicId);
        Assert.Equal(markdown, note.Markdown);
    }

    [Fact]
    public void Create_WithNullMarkdown_SetsEmptyMarkdown()
    {
        var result = TopicNote.Create(_ownerId, _topicId, null);

        Assert.True(result.IsOk);
        Assert.Equal(string.Empty, result.Data.Markdown);
    }

    [Fact]
    public void Create_WithMarkdownExceedingMaxLength_ReturnsInvalid()
    {
        var longMarkdown = new string('m', TopicNote.MaxMarkdownLength + 1);

        var result = TopicNote.Create(_ownerId, _topicId, longMarkdown);

        Assert.False(result.IsOk);
        Assert.Equal("planning.note.too_long", result.Failure.Messages[0].Code);
        Assert.Equal(nameof(TopicNote.Markdown), result.Failure.Messages[0].Target);
    }

    [Fact]
    public void Update_WithValidMarkdown_UpdatesMarkdown()
    {
        var note = TopicNote.Create(_ownerId, _topicId, "Initial").Data;

        var result = note.Update("Updated Markdown");

        Assert.True(result.IsOk);
        Assert.Equal("Updated Markdown", note.Markdown);
    }

    [Fact]
    public void Update_WithMarkdownExceedingMaxLength_ReturnsInvalid()
    {
        var note = TopicNote.Create(_ownerId, _topicId, "Initial").Data;
        var longMarkdown = new string('m', TopicNote.MaxMarkdownLength + 1);

        var result = note.Update(longMarkdown);

        Assert.False(result.IsOk);
        Assert.Equal("planning.note.too_long", result.Failure.Messages[0].Code);
    }

    [Fact]
    public void ParameterlessConstructor_InitializesEmpty()
    {
        var constructor = typeof(TopicNote).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        Assert.NotNull(constructor);
        var note = (TopicNote)constructor.Invoke(null);
        Assert.Equal(string.Empty, note.Markdown);
    }
}
