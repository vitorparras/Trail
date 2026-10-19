namespace Trail.Application.Accounts;

public interface IAccountEmailContent
{
    EmailMessage EmailConfirmation(string toAddress, string confirmationLink);

    EmailMessage PasswordReset(string toAddress, string resetLink);

    EmailMessage Invitation(string toAddress, string invitationLink);

    EmailMessage Test(string toAddress);
}
