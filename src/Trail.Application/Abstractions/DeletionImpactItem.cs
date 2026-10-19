namespace Trail.Application.Abstractions;

public sealed record DeletionImpactItem
{
    private DeletionImpactItem(string code, int count)
    {
        Code = code;
        Count = count;
    }

    public string Code { get; }

    public int Count { get; }

    public static DeletionImpactItem Create(string code, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return new DeletionImpactItem(code, count);
    }
}
