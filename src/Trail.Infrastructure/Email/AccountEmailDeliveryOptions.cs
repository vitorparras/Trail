namespace Trail.Infrastructure.Email;

public sealed class AccountEmailDeliveryOptions
{
    public int Capacity { get; set; } = 500;

    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
