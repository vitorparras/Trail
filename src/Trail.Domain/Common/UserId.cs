namespace Trail.Domain.Common;

public readonly record struct UserId(Guid Value) : ITypedId<UserId>
{
    public Guid Value { get; } = Value;

    public static UserId New() => new(Guid.CreateVersion7());

    public static UserId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
