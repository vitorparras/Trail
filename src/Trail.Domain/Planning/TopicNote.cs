
using Trail.Domain.Common;

namespace Trail.Domain.Planning;

public sealed class TopicNote : AggregateRoot<TopicNoteId>
{
    public const int MaxMarkdownLength = 50000;

    private TopicNote()
    {
    }

    private TopicNote(TopicNoteId id, UserId ownerId, TopicId topicId, string markdown)
        : base(id, ownerId)
    {
        TopicId = topicId;
        Markdown = markdown;
    }

    public TopicId TopicId { get; private init; }

    public string Markdown { get; private set; } = string.Empty;

    public static Result<TopicNote> Create(UserId ownerId, TopicId topicId, string? markdown)
    {
        var content = markdown ?? string.Empty;
        if (content.Length > MaxMarkdownLength)
        {
            return Result.Invalid(Message.Error("planning.note.too_long", nameof(Markdown)));
        }

        return Result.Ok(new TopicNote(TopicNoteId.New(), ownerId, topicId, content));
    }

    public Result Update(string? markdown)
    {
        var content = markdown ?? string.Empty;
        if (content.Length > MaxMarkdownLength)
        {
            return Result.Invalid(Message.Error("planning.note.too_long", nameof(Markdown)));
        }

        Markdown = content;
        return Result.Ok();
    }
}
