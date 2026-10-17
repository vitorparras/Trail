namespace Trail.Domain.Common;

public interface IResultEnvelope
{
    ResultStatus Status { get; }

    IReadOnlyList<Message> Messages { get; }
}
