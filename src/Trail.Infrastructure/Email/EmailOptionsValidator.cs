using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Trail.Infrastructure.Email;

public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            return ValidateOptionsResult.Success;
        }

        if (options.Port is < 1 or > 65535)
        {
            return ValidateOptionsResult.Fail("Email:Smtp:Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress) || !MailAddress.TryCreate(options.FromAddress, out _))
        {
            return ValidateOptionsResult.Fail("Email:Smtp:FromAddress must be a valid email address.");
        }

        return ValidateOptionsResult.Success;
    }
}
