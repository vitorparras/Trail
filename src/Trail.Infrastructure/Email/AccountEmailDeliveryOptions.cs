namespace Trail.Infrastructure.Email;

public sealed class AccountEmailDeliveryOptions
{
    public int Capacity { get; set; } = 500;

    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan CoalescingWindow { get; set; } = TimeSpan.FromMinutes(15);

    public int CoalescingCapacity { get; set; } = 10_000;
}
