using Trail.Application.Abstractions;
using Trail.Domain.Common;

namespace Trail.Application.Accounts;

public sealed record GenerateManualAccountLinkCommand(
    UserId TargetUserId,
    ManualLinkKind Kind,
    Func<UserId, string, string>? LinkBuilder = null);

public sealed class GenerateManualAccountLinkHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IAccountIdentityGateway _identityGateway;

    public GenerateManualAccountLinkHandler(
        ICurrentUser currentUser,
        IAccountIdentityGateway identityGateway)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(identityGateway);

        _currentUser = currentUser;
        _identityGateway = identityGateway;
    }

    public async Task<Result<ManualAccountLinkDto>> HandleAsync(
        GenerateManualAccountLinkCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.IsAuthenticated || !_currentUser.IsAdmin)
        {
            return Result.Forbidden(Message.Error("accounts.admin.forbidden"));
        }

        var targetUser = await _identityGateway.GetUserByIdAsync(command.TargetUserId, cancellationToken);
        if (targetUser is null)
        {
            return Result.NotFound(Message.Error("accounts.admin.user_not_found"));
        }

        if (command.Kind == ManualLinkKind.EmailConfirmation)
        {
            var tokenResult = await _identityGateway.GenerateEmailConfirmationTokenAsync(command.TargetUserId, cancellationToken);
            if (!tokenResult.IsOk)
            {
                return tokenResult.Failure;
            }

            var url = command.LinkBuilder?.Invoke(command.TargetUserId, tokenResult.Data)
                ?? $"/Account/ConfirmEmail?userId={command.TargetUserId}&token={Uri.EscapeDataString(tokenResult.Data)}";

            return Result.Ok(new ManualAccountLinkDto(command.TargetUserId, command.Kind, tokenResult.Data, url));
        }

        var resetTokenResult = await _identityGateway.GeneratePasswordResetTokenAsync(command.TargetUserId, cancellationToken);
        if (!resetTokenResult.IsOk)
        {
            return resetTokenResult.Failure;
        }

        var resetUrl = command.LinkBuilder?.Invoke(command.TargetUserId, resetTokenResult.Data)
            ?? $"/Account/ResetPassword?userId={command.TargetUserId}&token={Uri.EscapeDataString(resetTokenResult.Data)}";

        return Result.Ok(new ManualAccountLinkDto(command.TargetUserId, command.Kind, resetTokenResult.Data, resetUrl));
    }
}
