using Trail.Application.Abstractions;

namespace Trail.Application.Tests.Planning;

public sealed class DeletionImpactItemTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Create_WithCodeAndNonNegativeCount_KeepsBoth(int count)
    {
        var item = DeletionImpactItem.Create("practice.deletion.question_entries", count);

        Assert.Equal("practice.deletion.question_entries", item.Code);
        Assert.Equal(count, item.Count);
        Assert.Equal(DeletionImpactItem.Create("practice.deletion.question_entries", count), item);
    }

    [Fact]
    public void Create_WithNegativeCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DeletionImpactItem.Create("planning.deletion.topics", -1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutCode_Throws(string? code)
    {
        Assert.ThrowsAny<ArgumentException>(() => DeletionImpactItem.Create(code!, 1));
    }
}
