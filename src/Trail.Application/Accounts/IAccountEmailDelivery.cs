namespace Trail.Application.Accounts;

public interface IAccountEmailDelivery
{
    void Schedule(EmailMessage message);
}
