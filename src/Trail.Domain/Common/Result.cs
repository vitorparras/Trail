namespace Trail.Domain.Common;

public sealed class Result : IResultEnvelope
{
    public const string ConcurrencyConflictCode = "Concurrency.Conflict";

    private readonly Failure? _failure;

    private Result(IReadOnlyList<Message> messages) => Messages = messages;

    private Result(Failure failure)
    {
        _failure = failure;
        Messages = failure.Messages;
    }

    public ResultStatus Status => _failure?.Status ?? ResultStatus.Ok;

    public IReadOnlyList<Message> Messages { get; }

    public bool IsOk => _failure is null;

    public Failure Failure => _failure ?? throw new InvalidOperationException("Failure is only available when the status is not Ok.");

    public static Result Ok(params IEnumerable<Message> messages) => new(OkMessages(messages));

    public static Result Ok(IReadOnlyList<Message> messages) => new(OkMessages(messages));

    public static Result<T> Ok<T>(T data, params IEnumerable<Message> messages)
        where T : notnull => new(data, OkMessages(messages));

    public static Failure Invalid(params IEnumerable<Message> messages)
    {
        var copy = Copy(messages);

        if (!copy.Any(message => message.Severity is MessageSeverity.Error))
        {
            throw new ArgumentException("An Invalid result requires at least one error message.", nameof(messages));
        }

        return new(ResultStatus.Invalid, copy);
    }

    public static Failure NotFound(params IEnumerable<Message> messages) => new(ResultStatus.NotFound, Copy(messages));

    public static Failure Conflict(params IEnumerable<Message> messages) => new(ResultStatus.Conflict, Copy(messages));

    public static Failure ConcurrencyConflict() => Conflict(Message.Warning(ConcurrencyConflictCode));

    public static Failure Forbidden(params IEnumerable<Message> messages) => new(ResultStatus.Forbidden, Copy(messages));

    public TOut Match<TOut>(Func<TOut> onOk, Func<Failure, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onOk);
        ArgumentNullException.ThrowIfNull(onFailure);

        return _failure is null ? onOk() : onFailure(_failure);
    }

    public static implicit operator Result(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return new(failure);
    }

    private static IReadOnlyList<Message> OkMessages(IEnumerable<Message> messages)
    {
        var copy = Copy(messages);

        if (copy.Any(message => message.Severity is MessageSeverity.Error))
        {
            throw new ArgumentException("An Ok result cannot carry error messages.", nameof(messages));
        }

        return copy;
    }

    private static IReadOnlyList<Message> Copy(IEnumerable<Message> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        return [.. messages];
    }
}
