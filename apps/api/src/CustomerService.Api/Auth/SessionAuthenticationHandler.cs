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

    /// <summary>Returns the token of a "Bearer" header (scheme matched case-insensitively), or an empty string.</summary>
    public static string ExtractBearerToken(string header) =>
        header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? header[BearerPrefix.Length..].Trim()
            : string.Empty;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var values) || values.Count == 0)
        {
            return AuthenticateResult.NoResult();
        }

        var token = ExtractBearerToken(values.ToString());
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
