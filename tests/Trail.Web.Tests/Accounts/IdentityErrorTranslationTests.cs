using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Trail.Infrastructure.Identity;
using Trail.Web.Results;

namespace Trail.Web.Tests.Accounts;

public sealed class IdentityErrorTranslationTests(TrailWebApplicationFactory factory) : IClassFixture<TrailWebApplicationFactory>
{
    private const string SampleEmail = "sample@trail.local";

    private IdentityOptions Options => factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;

    [Fact]
    public void ReachableIdentityErrors_WithTheConfiguredOptions_HaveTheirOwnTranslatedCode()
    {
        var errors = ReachableErrors();

        var genericOrUntranslated = errors
            .Where(error => error.Code != nameof(IdentityErrorDescriber.DefaultError))
            .Where(error => IdentityErrorMessages.CodeFor(error) == IdentityErrorMessages.DefaultErrorCode || !IsTranslated(IdentityErrorMessages.CodeFor(error)))
            .Select(error => error.Code)
            .ToList();

        Assert.NotEmpty(errors);
        Assert.Empty(genericOrUntranslated);
    }

    [Fact]
    public void TranslatedErrorCodes_EachHaveATranslation()
    {
        var untranslated = IdentityErrorMessages.TranslatedErrorCodes
            .Select(code => IdentityErrorMessages.CodeFor(new IdentityError { Code = code }))
            .Where(code => !IsTranslated(code))
            .ToList();

        Assert.Empty(untranslated);
    }

    [Fact]
    public void CodeFor_IdentityCodeOutsideTheTranslatedSet_FallsBackToTheTranslatedDefaultError()
    {
        var code = IdentityErrorMessages.CodeFor(new IdentityError { Code = "UnknownIdentityFailure" });

        Assert.Equal(IdentityErrorMessages.DefaultErrorCode, code);
        Assert.True(IsTranslated(code));
    }

    [Fact]
    public void ToMessages_TargetsEachErrorAtItsField()
    {
        using var scope = factory.Services.CreateScope();
        var describer = scope.ServiceProvider.GetRequiredService<IdentityErrorDescriber>();
        var targets = new IdentityErrorTargets(Password: "NewPassword", CurrentPassword: "CurrentPassword", Email: "Email");

        var messages = IdentityErrorMessages.ToMessages(
            [describer.PasswordTooShort(6), describer.PasswordMismatch(), describer.InvalidEmail(SampleEmail), describer.ConcurrencyFailure()],
            targets);

        string?[] expected = ["NewPassword", "CurrentPassword", "Email", null];
        Assert.Equal(expected, messages.Select(message => message.Target));
    }

    [Fact]
    public void PasswordTooShortText_StatesTheConfiguredMinimumLength()
    {
        var localizer = factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();

        var text = localizer["accounts.identity.password_too_short"].Value;

        Assert.Matches($@"\b{Options.Password.RequiredLength.ToString(CultureInfo.InvariantCulture)}\b", text);
    }

    [Fact]
    public void InvalidUserNameText_DescribesTheConfiguredUserNameCharacters()
    {
        Assert.Equal(new UserOptions().AllowedUserNameCharacters, Options.User.AllowedUserNameCharacters);
    }

    private bool IsTranslated(string code) =>
        !factory.Services.GetRequiredService<IStringLocalizerFactory>().CreateForCode(code)[code].ResourceNotFound;

    private List<IdentityError> ReachableErrors()
    {
        using var scope = factory.Services.CreateScope();
        var describer = scope.ServiceProvider.GetRequiredService<IdentityErrorDescriber>();
        var options = Options;

        List<IdentityError> errors =
        [
            describer.DefaultError(),
            describer.ConcurrencyFailure(),
            describer.PasswordMismatch(),
            describer.InvalidToken(),
            describer.InvalidUserName(SampleEmail),
            describer.DuplicateUserName(SampleEmail),
            describer.PasswordTooShort(options.Password.RequiredLength),
        ];

        if (options.User.RequireUniqueEmail)
        {
            errors.Add(describer.InvalidEmail(SampleEmail));
            errors.Add(describer.DuplicateEmail(SampleEmail));
        }

        if (options.Password.RequiredUniqueChars >= 1)
        {
            errors.Add(describer.PasswordRequiresUniqueChars(options.Password.RequiredUniqueChars));
        }

        if (options.Password.RequireNonAlphanumeric)
        {
            errors.Add(describer.PasswordRequiresNonAlphanumeric());
        }

        if (options.Password.RequireDigit)
        {
            errors.Add(describer.PasswordRequiresDigit());
        }

        if (options.Password.RequireLowercase)
        {
            errors.Add(describer.PasswordRequiresLower());
        }

        if (options.Password.RequireUppercase)
        {
            errors.Add(describer.PasswordRequiresUpper());
        }

        return errors;
    }
}
