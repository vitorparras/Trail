using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace Trail.Web.Tests.Results;

public sealed class ResourceNameLocalizationWebApplicationFactory : TrailWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IStringLocalizerFactory, ResourceNameLocalizerFactory>()));
    }
}

public sealed class ResourceNameLocalizerFactory : IStringLocalizerFactory
{
    public IStringLocalizer Create(Type resourceSource)
    {
        ArgumentNullException.ThrowIfNull(resourceSource);

        return new ResourceNameLocalizer(resourceSource.Name);
    }

    public IStringLocalizer Create(string baseName, string location) => new ResourceNameLocalizer(baseName);
}

public sealed class ResourceNameLocalizer(string resourceName) : IStringLocalizer
{
    public LocalizedString this[string name] => new(name, TextFor(resourceName, name));

    public LocalizedString this[string name, params object[] arguments] => this[name];

    public static string TextFor(string resourceName, string name) => $"{resourceName}:{name}";

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
