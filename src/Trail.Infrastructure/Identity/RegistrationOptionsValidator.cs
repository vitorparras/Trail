using Microsoft.Extensions.Options;

namespace Trail.Infrastructure.Identity;

public sealed class RegistrationOptionsValidator : IValidateOptions<RegistrationOptions>
{
    public ValidateOptionsResult Validate(string? name, RegistrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.InvitationExpirationHours <= 0)
        {
            return ValidateOptionsResult.Fail("Registration:InvitationExpirationHours must be greater than zero.");
        }

        return ValidateOptionsResult.Success;
    }
}
