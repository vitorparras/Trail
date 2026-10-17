using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Trail.Application;
using Trail.Infrastructure;
using Trail.Web.Cli;
using Trail.Web.Htmx;
using Trail.Web.Observability;
using Trail.Web.Security;

if (args is ["--healthcheck", ..])
{
    return await HealthCheckCommand.RunAsync(args);
}

var builder = WebApplication.CreateBuilder(args);

builder.AddTrailObservability();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var culture = CultureInfo.GetCultureInfo("pt-BR");

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(culture);
    options.SupportedCultures = [culture];
    options.SupportedUICultures = [culture];
    options.RequestCultureProviders.Clear();
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddPageApplicationModelConvention("/Account/Login", page => page.EndpointMetadata.Add(new EnableRateLimitingAttribute(RateLimitingExtensions.AuthPolicyName)));
    options.Conventions.AddPageApplicationModelConvention("/Account/ForgotPassword", page => page.EndpointMetadata.Add(new EnableRateLimitingAttribute(RateLimitingExtensions.AuthPolicyName)));
});

var htmlEncoderSettings = new TextEncoderSettings(
    UnicodeRanges.BasicLatin,
    UnicodeRanges.Latin1Supplement,
    UnicodeRanges.LatinExtendedA,
    UnicodeRanges.GeneralPunctuation);
htmlEncoderSettings.ForbidRange(new UnicodeRange(0x200B, 5));
htmlEncoderSettings.ForbidRange(new UnicodeRange(0x2028, 7));
htmlEncoderSettings.ForbidRange(new UnicodeRange(0x2060, 16));
builder.Services.Replace(ServiceDescriptor.Singleton(HtmlEncoder.Create(htmlEncoderSettings)));

builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);
builder.Services.AddHtmx();

builder.Services.AddScoped<ICspNonceService, CspNonceService>();
builder.Services.AddTrailRateLimiting(builder.Configuration);
builder.Services.AddOptions<RepositoryOptions>()
    .BindConfiguration(RepositoryOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<RepositoryOptions>, RepositoryOptionsValidator>();

var app = builder.Build();

app.UseExceptionHandler("/Error");
app.UseSecurityHeaders();
app.UseRequestLocalization();
app.UseRouting();
app.UseRateLimiter();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.Run();

return 0;
