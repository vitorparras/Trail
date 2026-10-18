using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Trail.Domain.Common;

namespace Trail.Infrastructure.Identity;

internal sealed record IdentityErrorTargets(string? Password = null, string? CurrentPassword = null, string? Email = null);

internal static class IdentityErrorMessages
{
    public const string DefaultErrorCode = "accounts.identity.default_error";

    private const string CodePrefix = "accounts.identity.";

    public static IReadOnlySet<string> TranslatedErrorCodes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(IdentityErrorDescriber.DefaultError),
        nameof(IdentityErrorDescriber.ConcurrencyFailure),
        nameof(IdentityErrorDescriber.PasswordMismatch),
        nameof(IdentityErrorDescriber.InvalidToken),
        nameof(IdentityErrorDescriber.InvalidUserName),
        nameof(IdentityErrorDescriber.InvalidEmail),
        nameof(IdentityErrorDescriber.DuplicateUserName),
        nameof(IdentityErrorDescriber.DuplicateEmail),
        nameof(IdentityErrorDescriber.PasswordTooShort),
        nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars),
    };

    private static readonly HashSet<string> PasswordRuleCodes =
    [
        nameof(IdentityErrorDescriber.PasswordTooShort),
        nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars),
        nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric),
        nameof(IdentityErrorDescriber.PasswordRequiresDigit),
        nameof(IdentityErrorDescriber.PasswordRequiresLower),
        nameof(IdentityErrorDescriber.PasswordRequiresUpper),
    ];

    private static readonly HashSet<string> EmailRuleCodes =
    [
        nameof(IdentityErrorDescriber.InvalidUserName),
        nameof(IdentityErrorDescriber.InvalidEmail),
    ];

    public static string CodeFor(IdentityError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Code is not null && TranslatedErrorCodes.Contains(error.Code)
            ? CodePrefix + JsonNamingPolicy.SnakeCaseLower.ConvertName(error.Code)
            : DefaultErrorCode;
    }

    public static bool IsInvalidToken(IdentityError error) =>
        error.Code == nameof(IdentityErrorDescriber.InvalidToken);

    public static bool IsDuplicateAccount(IdentityError error) =>
        error.Code is nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.DuplicateEmail);

    public static Message[] ToMessages(IEnumerable<IdentityError> errors, IdentityErrorTargets targets)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(targets);

        return [.. errors.Select(error => Message.Error(CodeFor(error), TargetFor(error, targets)))];
    }

    private static string? TargetFor(IdentityError error, IdentityErrorTargets targets)
    {
        if (PasswordRuleCodes.Contains(error.Code))
        {
            return targets.Password;
        }

        if (EmailRuleCodes.Contains(error.Code))
        {
            return targets.Email;
        }

        return error.Code == nameof(IdentityErrorDescriber.PasswordMismatch) ? targets.CurrentPassword : null;
    }
}
