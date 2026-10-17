namespace Trail.Infrastructure.Identity;

public class AccountInvitation
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public Guid InvitedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public Guid? AcceptedByUserId { get; set; }
}
