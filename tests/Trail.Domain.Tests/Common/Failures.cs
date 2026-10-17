using Trail.Domain.Common;

namespace Trail.Domain.Tests.Common;

internal static class Failures
{
    public static readonly Message Error = Message.Error("Sample.Invalid", "Field");

    public static Failure Create(ResultStatus status) => status switch
    {
        ResultStatus.Invalid => Result.Invalid(Error),
        ResultStatus.NotFound => Result.NotFound(Error),
        ResultStatus.Conflict => Result.Conflict(Error),
        ResultStatus.Forbidden => Result.Forbidden(Error),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
