using System.Diagnostics.Metrics;
using Trail.Application.Accounts;
using Trail.Application.Telemetry;
using Trail.Domain.Common;

namespace Trail.Application.Tests.Accounts;

public sealed class ResendEmailConfirmationHandlerTests
{
    private readonly FakeAccountIdentityGateway _identityGateway = new();
    private readonly FakeEmailSender _emailSender = new();
    private readonly ApplicationMetrics _metrics = new(new RecordingMeterFactory());

    private ResendEmailConfirmationHandler CreateHandler() =>
        new(_identityGateway, _emailSender, _metrics);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_MissingEmail_ReturnsInvalid(string? email)
    {
        var handler = CreateHandler();
        var command = new ResendEmailConfirmationCommand(email!);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsOk);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal("accounts.resend_confirmation.email_required", message.Code);
    }

    [Fact]
    public async Task HandleAsync_UserNotFound_ReturnsOkToPreventEnumeration()
    {
        var handler = CreateHandler();
        var command = new ResendEmailConfirmationCommand("notfound@example.com");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(_emailSender.SentMessages);
    }

    [Fact]
    public async Task HandleAsync_UserAlreadyConfirmed_ReturnsOk()
    {
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "confirmed@example.com", false, true, true, DateTime.UtcNow));
        var handler = CreateHandler();
        var command = new ResendEmailConfirmationCommand("confirmed@example.com");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(_emailSender.SentMessages);
    }

    [Fact]
    public async Task HandleAsync_UserInactive_ReturnsOk()
    {
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "inactive@example.com", false, false, false, DateTime.UtcNow));
        var handler = CreateHandler();
        var command = new ResendEmailConfirmationCommand("inactive@example.com");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(_emailSender.SentMessages);
    }

    [Fact]
    public async Task HandleAsync_SmtpNotConfigured_ReturnsOk()
    {
        _emailSender.IsSmtpConfigured = false;
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "pending@example.com", false, true, false, DateTime.UtcNow));
        var handler = CreateHandler();
        var command = new ResendEmailConfirmationCommand("pending@example.com");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(_emailSender.SentMessages);
    }

    [Fact]
    public async Task HandleAsync_TokenGenerationFails_ReturnsTheSameOkAsAnUnknownEmail()
    {
        _identityGateway.EmailConfirmationTokenResult = Result.Invalid(Message.Error("token.error"));
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "pending@example.com", false, true, false, DateTime.UtcNow));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(new ResendEmailConfirmationCommand("pending@example.com"), TestContext.Current.CancellationToken);
        var unknown = await handler.HandleAsync(new ResendEmailConfirmationCommand("unknown@example.com"), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(result.Messages);
        Assert.Equal(unknown.Status, result.Status);
        Assert.Empty(_emailSender.SentMessages);
    }

    [Fact]
    public async Task HandleAsync_SendEmailFails_ReturnsTheSameOkAsAnUnknownEmailAndRecordsTheFailure()
    {
        _emailSender.SendResult = Result.Invalid(Message.Error("smtp.offline"));
        _identityGateway.Users.Add(new AccountUserDto(UserId.New(), "pending@example.com", false, true, false, DateTime.UtcNow));
        var handler = CreateHandler();
        long failures = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == _metrics.Meter && instrument.Name == ApplicationMetrics.EmailsFailedInstrumentName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => failures += measurement);
        listener.Start();

        var result = await handler.HandleAsync(new ResendEmailConfirmationCommand("pending@example.com"), TestContext.Current.CancellationToken);
        var unknown = await handler.HandleAsync(new ResendEmailConfirmationCommand("unknown@example.com"), TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        Assert.Empty(result.Messages);
        Assert.Equal(unknown.Status, result.Status);
        Assert.Equal(1, failures);
    }

    [Fact]
    public async Task HandleAsync_SendEmailSucceeds_SendsEmailAndReturnsOk()
    {
        var userId = UserId.New();
        _identityGateway.Users.Add(new AccountUserDto(userId, "pending@example.com", false, true, false, DateTime.UtcNow));
        var handler = CreateHandler();
        var command = new ResendEmailConfirmationCommand(
            "pending@example.com",
            ConfirmationLinkBuilder: (id, token) => $"https://trail.app/confirm/{id}/{token}");

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsOk);
        var message = Assert.Single(_emailSender.SentMessages);
        Assert.Equal("pending@example.com", message.ToAddress);
        Assert.Contains($"https://trail.app/confirm/{userId}/sample-confirm-token", message.HtmlBody);
    }

    private sealed class RecordingMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var meter in _meters)
            {
                meter.Dispose();
            }
        }
    }
}
