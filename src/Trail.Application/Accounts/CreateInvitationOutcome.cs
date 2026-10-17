namespace Trail.Application.Accounts;

public sealed record CreateInvitationOutcome(
    string Email,
    string InvitationToken,
    string InvitationLink,
    bool EmailSent);
