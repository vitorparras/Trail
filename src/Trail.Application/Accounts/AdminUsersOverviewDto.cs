namespace Trail.Application.Accounts;

public sealed record AdminUsersOverviewDto(
    IReadOnlyList<AccountUserDto> Users,
    IReadOnlyList<AccountInvitationDto> Invitations,
    RegistrationMode RegistrationMode);
