using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record RegisterAccountOutcome(
    bool RequiresEmailConfirmation,
    UserId? UserId = null);
