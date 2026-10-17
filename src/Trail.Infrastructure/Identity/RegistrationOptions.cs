using Trail.Application.Accounts;

namespace Trail.Infrastructure.Identity;

public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    public RegistrationMode Mode { get; set; } = RegistrationMode.Closed;

    public int InvitationExpirationHours { get; set; } = 72;
}
