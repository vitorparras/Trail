using System.Reflection;
using Trail.Domain.Common;

namespace Trail.Domain.Tests.Common;

public sealed class TypedIdConventionTests
{
    public static TheoryData<Type> TypedIds => new(
        typeof(UserId).Assembly
            .GetTypes()
            .Where(type => type.GetInterfaces().Any(IsTypedId)));

    [Theory]
    [MemberData(nameof(TypedIds))]
    public void TypedId_EveryImplementationInTheDomain_FollowsTheConvention(Type typedId)
    {
        var value = Guid.CreateVersion7();
        var valueProperty = typedId.GetProperty(nameof(UserId.Value))!;
        var from = typedId.GetMethod(nameof(UserId.From), BindingFlags.Public | BindingFlags.Static, [typeof(Guid)])!;
        var create = typedId.GetMethod(nameof(UserId.New), BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);

        var wrapped = from.Invoke(null, [value])!;

        Assert.False(valueProperty.CanWrite);
        Assert.Equal(value, valueProperty.GetValue(wrapped));
        Assert.Equal(value.ToString(), wrapped.ToString());
        Assert.NotNull(create);
        Assert.Equal(typedId, create.ReturnType);

        var created = Enumerable.Range(0, 1_000)
            .Select(_ => (Guid)valueProperty.GetValue(create.Invoke(null, null))!)
            .ToList();

        Assert.All(created, guid => Assert.Equal(7, guid.Version));
        Assert.Equal(created.Count, created.Distinct().Count());
    }

    private static bool IsTypedId(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ITypedId<>);
}
