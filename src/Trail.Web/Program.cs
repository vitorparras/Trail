using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trail.Application;
using Trail.Web.Cli;
using Trail.Web.Htmx;
using Trail.Web.Observability;

if (args is ["--healthcheck", ..])
{
    return await HealthCheckCommand.RunAsync(args);
}

var builder = WebApplication.CreateBuilder(args);

builder.AddTrailObservability();
builder.Services.AddApplication();

var culture = CultureInfo.GetCultureInfo("pt-BR");

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(culture);
    options.SupportedCultures = [culture];
    options.SupportedUICultures = [culture];
    options.RequestCultureProviders.Clear();
});
builder.Services.AddRazorPages();

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

var app = builder.Build();

app.UseExceptionHandler("/Error");
app.UseRequestLocalization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();

return 0;
