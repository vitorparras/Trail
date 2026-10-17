namespace Trail.Application.Accounts;

public sealed record AccountInvitationDto(
    Guid Id,
    string Email,
    string Token,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsAccepted,
    DateTime? AcceptedAtUtc);
