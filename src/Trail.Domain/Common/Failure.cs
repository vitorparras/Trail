namespace Trail.Domain.Common;

public sealed class Failure : IResultEnvelope
{
    internal Failure(ResultStatus status, IReadOnlyList<Message> messages)
    {
        Status = status;
        Messages = messages;
    }

    public ResultStatus Status { get; }

    public IReadOnlyList<Message> Messages { get; }
}
