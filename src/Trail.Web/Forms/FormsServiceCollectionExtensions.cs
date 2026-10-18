using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Trail.Web.Forms;

public static class FormsServiceCollectionExtensions
{
    public static IServiceCollection AddFormValidationMessages(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IConfigureOptions<MvcOptions>, FormValidationMessagesSetup>();
        services.Configure<MvcViewOptions>(options => options.HtmlHelperOptions.ClientValidationEnabled = false);
        return services;
    }

    private sealed class FormValidationMessagesSetup(IStringLocalizer<SharedResource> localizer) : IConfigureOptions<MvcOptions>
    {
        public void Configure(MvcOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            options.Filters.Add(new BindingErrorsPageFilter());

            var messages = options.ModelBindingMessageProvider;
            messages.SetAttemptedValueIsInvalidAccessor((value, _) => Text("ModelBindingAttemptedValueIsInvalid", value));
            messages.SetMissingBindRequiredValueAccessor(_ => Text("ModelBindingMissingBindRequiredValue"));
            messages.SetMissingKeyOrValueAccessor(() => Text("ModelBindingMissingKeyOrValue"));
            messages.SetMissingRequestBodyRequiredValueAccessor(() => Text("ModelBindingMissingRequestBodyRequiredValue"));
            messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => Text("ModelBindingAttemptedValueIsInvalid", value));
            messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => Text("ModelBindingUnknownValueIsInvalid"));
            messages.SetNonPropertyValueMustBeANumberAccessor(() => Text("ModelBindingValueMustBeANumber"));
            messages.SetUnknownValueIsInvalidAccessor(_ => Text("ModelBindingUnknownValueIsInvalid"));
            messages.SetValueIsInvalidAccessor(value => Text("ModelBindingAttemptedValueIsInvalid", value));
            messages.SetValueMustBeANumberAccessor(_ => Text("ModelBindingValueMustBeANumber"));
            messages.SetValueMustNotBeNullAccessor(_ => Text("ModelBindingValueMustNotBeNull"));
        }

        private string Text(string key, params object[] arguments) => localizer[key, arguments].Value;
    }
}
