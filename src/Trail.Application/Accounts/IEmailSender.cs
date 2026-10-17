using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public interface IEmailSender
{
    bool IsSmtpConfigured { get; }

    Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
