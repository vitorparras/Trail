using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Trail.Application;
using Trail.Application.Abstractions;
using Trail.Application.Accounts;
using Trail.Infrastructure;
using Trail.Web;
using Trail.Web.Accounts;
using Trail.Web.Auth;
using Trail.Web.Cli;
using Trail.Web.Fitness;
using Trail.Web.Forms;
using Trail.Web.Htmx;
using Trail.Web.Markdown;
using Trail.Web.Observability;
using Trail.Web.Practice;
using Trail.Web.Pwa;
using Trail.Web.Results;
using Trail.Web.Security;
using Trail.Web.StudyTime;

if (args is ["--healthcheck", ..])
{
    return await HealthCheckCommand.RunAsync(args);
}

var builder = WebApplication.CreateBuilder(args);

builder.AddTrailObservability();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.Replace(ServiceDescriptor.Scoped<ICurrentUser, HttpCurrentUser>());
builder.Services.Replace(ServiceDescriptor.Singleton<IAccountEmailContent, LocalizedAccountEmailContent>());

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
    options.Conventions.ConfigureFilter(new UnknownPageHandlerFilter());
});
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

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
builder.Services.AddFormValidationMessages();
builder.Services.AddSingleton<IMarkdownRenderer, MarkdigMarkdownRenderer>();

builder.Services.AddScoped<ICspNonceService, CspNonceService>();
builder.Services.AddTrailRateLimiting(builder.Configuration);
builder.Services.AddOptions<RepositoryOptions>()
    .BindConfiguration(RepositoryOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<RepositoryOptions>, RepositoryOptionsValidator>();

builder.Services.AddWebContributorsFrom(typeof(Program).Assembly);
builder.Services.AddPracticeWeb();
builder.Services.AddFitnessWeb();
builder.Services.AddStudyTimeWeb();
builder.Services.AddPwaWeb();

if (args is ["--reset-admin-password", ..])
{
    var app = builder.Build();
    return await ResetAdminPasswordCommand.RunAsync(args, app.Services);
}

var webApp = builder.Build();

webApp.UseExceptionHandler("/Error");
webApp.UseSecurityHeaders();
webApp.UseRequestLocalization();
webApp.UseRouting();
webApp.UseAuthentication();
webApp.UseAuthorization();
webApp.UseRateLimiter();
webApp.MapStaticAssets().AllowAnonymous();
webApp.MapRazorPages().WithStaticAssets();

webApp.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
}).AllowAnonymous();

webApp.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
}).AllowAnonymous();

webApp.MapFallback("{*path}", () => Results.NotFound()).AllowAnonymous();

webApp.Run();

return 0;
