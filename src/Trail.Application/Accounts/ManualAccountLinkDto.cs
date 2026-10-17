using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record ManualAccountLinkDto(
    UserId TargetUserId,
    ManualLinkKind Kind,
    string Token,
    string Url);
