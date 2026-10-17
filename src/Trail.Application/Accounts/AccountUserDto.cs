using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record AccountUserDto(
    UserId Id,
    string Email,
    bool IsAdmin,
    bool IsActive,
    bool EmailConfirmed,
    DateTime CreatedAtUtc);
