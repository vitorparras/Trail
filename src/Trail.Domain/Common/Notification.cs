namespace Trail.Domain.Common;

public sealed class Notification
{
    private readonly List<Message> _messages = [];

    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();

    public bool HasErrors => _messages.Exists(message => message.Severity is MessageSeverity.Error);

    public Notification Add(params IEnumerable<Message> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        _messages.AddRange(messages);
        return this;
    }

    public Notification AddErrorIf(bool condition, string code, string? target = null) =>
        condition ? Add(Message.Error(code, target)) : this;

    public Result ToResult() => HasErrors ? Result.Invalid(_messages) : Result.Ok(Messages);

    public Result<T> ToResult<T>(Func<T> createData)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(createData);

        return HasErrors ? Result.Invalid(_messages) : Result.Ok(createData(), _messages);
    }
}
