using System.Diagnostics;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Trail.Application.Accounts;
using Trail.Application.Telemetry;
using Trail.Domain.Common;

namespace Trail.Infrastructure.Email;

public sealed partial class MailKitEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(
        IOptions<EmailOptions> options,
        ILogger<MailKitEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
    }

    public bool IsSmtpConfigured => !string.IsNullOrWhiteSpace(_options.Host);

    public async Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var activity = ApplicationTelemetry.ActivitySource.StartActivity("trail.email.send", ActivityKind.Client);
        activity?.SetTag("trail.email.kind", message.Kind);

        if (!IsSmtpConfigured)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "SMTP is not configured");
            return Result.Invalid(Message.Error("accounts.email.smtp_not_configured", "SMTP host is not configured."));
        }

        try
        {
            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            mimeMessage.To.Add(MailboxAddress.Parse(message.ToAddress));
            mimeMessage.Subject = message.Subject;

            var body = new MultipartAlternative
            {
                new TextPart("plain") { Text = message.TextBody },
                new TextPart("html") { Text = message.HtmlBody },
            };
            mimeMessage.Body = body;

            using var client = new SmtpClient();

            var secureSocketOptions = _options.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : (_options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

            await client.ConnectAsync(_options.Host!, _options.Port, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            }

            await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
            return Result.Ok();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogEmailSendFailed(_logger, ex, message.ToAddress, message.Kind);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result.Invalid(Message.Error("accounts.email.send_failed", "Failed to send email."));
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Failed to send email of kind {Kind} to {Recipient}")]
    private static partial void LogEmailSendFailed(
        ILogger logger,
        Exception exception,
        string recipient,
        string kind);
}
