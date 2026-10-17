using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Trail.Application.Abstractions;

namespace Trail.Web.Tests.Planning;

public sealed class TestCurrentUserAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ICurrentUser currentUser)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestCurrentUser";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!currentUser.IsAuthenticated)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, currentUser.Id.Value.ToString()) };
        if (currentUser.IsAdmin)
        {
            claims.Add(new Claim("trail:admin", "true"));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
