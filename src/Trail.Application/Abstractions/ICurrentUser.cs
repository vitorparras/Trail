using Trail.Domain.Common;

namespace Trail.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    UserId Id { get; }
}
