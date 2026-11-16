using System.Security.Claims;
using System.Text.Encodings.Web;
using CustomerService.Api.Data;
using CustomerService.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CustomerService.Api.Auth;

public class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IUserRepository users) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "Session";

    private const string BearerPrefix = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var values) || values.Count == 0)
        {
            return AuthenticateResult.NoResult();
        }

        var header = values.ToString();
        var token = header.StartsWith(BearerPrefix, StringComparison.Ordinal)
            ? header[BearerPrefix.Length..].Trim()
            : string.Empty;
        if (token.Length == 0)
        {
            return AuthenticateResult.Fail("Malformed Authorization header.");
        }

        var user = await users.FindBySessionAsync(AuthManager.HashToken(token), Context.RequestAborted);
        if (user is null)
        {
            return AuthenticateResult.Fail("Unknown session.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        if (user.MustChangePassword)
        {
            claims.Add(new Claim(PasswordChangedRequirement.ClaimType, "true"));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
