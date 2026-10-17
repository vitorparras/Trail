using Microsoft.Extensions.Options;

namespace Trail.Web.Security;

public sealed class RepositoryOptions : IValidateOptions<RepositoryOptions>
{
    public const string SectionName = "Repository";

    public string SourceCodeUrl { get; set; } = "https://github.com/vitorparras/trail";

    public ValidateOptionsResult Validate(string? name, RepositoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.SourceCodeUrl))
        {
            return ValidateOptionsResult.Fail("Repository:SourceCodeUrl is required.");
        }

        if (!Uri.TryCreate(options.SourceCodeUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return ValidateOptionsResult.Fail($"Repository:SourceCodeUrl '{options.SourceCodeUrl}' is not a valid absolute http or https URI.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class RepositoryOptionsValidator : IValidateOptions<RepositoryOptions>
{
    public ValidateOptionsResult Validate(string? name, RepositoryOptions options)
    {
        return new RepositoryOptions().Validate(name, options);
    }
}
