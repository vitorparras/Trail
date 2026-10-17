namespace Trail.Domain.Common;

public sealed class Result<T> : IResultEnvelope
    where T : notnull
{
    private readonly T _data;
    private readonly Failure? _failure;

    internal Result(T data, IReadOnlyList<Message> messages)
    {
        _data = data;
        Messages = messages;
    }

    private Result(Failure failure)
    {
        _data = default!;
        _failure = failure;
        Messages = failure.Messages;
    }

    public ResultStatus Status => _failure?.Status ?? ResultStatus.Ok;

    public IReadOnlyList<Message> Messages { get; }

    public bool IsOk => _failure is null;

    public T Data => _failure is null
        ? _data
        : throw new InvalidOperationException($"Data is only available when the status is Ok, but it is {_failure.Status}.");

    public Failure Failure => _failure ?? throw new InvalidOperationException("Failure is only available when the status is not Ok.");

    public Result<TOut> Map<TOut>(Func<T, TOut> map)
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(map);

        return _failure is null ? new Result<TOut>(map(_data), Messages) : _failure;
    }

    public TOut Match<TOut>(Func<T, TOut> onOk, Func<Failure, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onOk);
        ArgumentNullException.ThrowIfNull(onFailure);

        return _failure is null ? onOk(_data) : onFailure(_failure);
    }

    public static implicit operator Result<T>(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return new(failure);
    }
}
