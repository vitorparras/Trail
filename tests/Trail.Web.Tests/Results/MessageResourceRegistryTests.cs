using Microsoft.Extensions.Localization;
using Trail.Web.Results;

namespace Trail.Web.Tests.Results;

public sealed class MessageResourceRegistryTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    [Theory]
    [InlineData("practice.entry.date_in_future", typeof(PracticeResource))]
    [InlineData("fitness.attempt.not_found", typeof(FitnessResource))]
    [InlineData("studytime.session.overlap", typeof(StudyTimeResource))]
    [InlineData("pwa.offline.title", typeof(PwaResource))]
    [InlineData("planning.plan.not_found", typeof(SharedResource))]
    [InlineData("accounts.login.invalid_credentials", typeof(SharedResource))]
    [InlineData("Concurrency.Conflict", typeof(SharedResource))]
    [InlineData("practice", typeof(SharedResource))]
    [InlineData("Practice.entry.date_in_future", typeof(SharedResource))]
    [InlineData("practices.entry", typeof(SharedResource))]
    public void ResourceTypeFor_Code_ReturnsTheResourceOfItsPrefix(string code, Type expected)
    {
        Assert.Equal(expected, MessageResourceRegistry.ResourceTypeFor(code));
    }

    [Fact]
    public void ResourceTypes_ListSharedAndEveryModuleResource()
    {
        Assert.Equal(
            [typeof(SharedResource), typeof(PracticeResource), typeof(FitnessResource), typeof(StudyTimeResource), typeof(PwaResource)],
            MessageResourceRegistry.ResourceTypes);
    }

    [Fact]
    public void ResourceTypes_EachHasAnEmbeddedResxNamedAfterIt()
    {
        var embedded = typeof(SharedResource).Assembly.GetManifestResourceNames();

        Assert.All(
            MessageResourceRegistry.ResourceTypes,
            resourceType => Assert.Contains($"Trail.Web.Resources.{resourceType.Name}.resources", embedded));
    }

    [Fact]
    public void CreateForCode_SharedCode_UsesTheSharedResource()
    {
        var localizerFactory = factory.Services.GetRequiredService<IStringLocalizerFactory>();
        var shared = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

        var localized = localizerFactory.CreateForCode("planning.plan.not_found")["planning.plan.not_found"];

        Assert.False(localized.ResourceNotFound);
        Assert.Equal(shared["planning.plan.not_found"].Value, localized.Value);
    }
}
