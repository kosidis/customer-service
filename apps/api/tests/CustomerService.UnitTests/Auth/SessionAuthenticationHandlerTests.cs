using System.Security.Claims;
using System.Text.Encodings.Web;
using CustomerService.Api.Auth;
using CustomerService.Api.Data;
using CustomerService.Api.Domain;
using CustomerService.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CustomerService.UnitTests.Auth;

public class SessionAuthenticationHandlerTests
{
    private readonly Mock<IUserRepository> _users = new(MockBehavior.Strict);

    private async Task<AuthenticateResult> AuthenticateAsync(string? header)
    {
        var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Setup(o => o.Get(SessionAuthenticationHandler.SchemeName)).Returns(new AuthenticationSchemeOptions());
        var handler = new SessionAuthenticationHandler(options.Object, NullLoggerFactory.Instance, UrlEncoder.Default, _users.Object);
        var context = new DefaultHttpContext();
        if (header is not null)
        {
            context.Request.Headers.Authorization = header;
        }

        await handler.InitializeAsync(
            new AuthenticationScheme(SessionAuthenticationHandler.SchemeName, null, typeof(SessionAuthenticationHandler)),
            context);
        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task HandleAuthenticate_NoHeader_ReturnsNoResult()
    {
        var result = await AuthenticateAsync(null);

        Assert.True(result.None);
    }

    [Theory]
    [InlineData("Bearer ")]
    [InlineData("Bearer")]
    [InlineData("Basic abc")]
    [InlineData("abc")]
    public async Task HandleAuthenticate_MalformedHeader_Fails(string header)
    {
        var result = await AuthenticateAsync(header);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task HandleAuthenticate_UnknownToken_Fails()
    {
        _users.Setup(u => u.FindBySessionAsync(AuthManager.HashToken("tok"), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await AuthenticateAsync("Bearer tok");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task HandleAuthenticate_KnownToken_BuildsPrincipalWithoutPendingClaim()
    {
        _users.Setup(u => u.FindBySessionAsync(AuthManager.HashToken("tok"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 7, Role = Role.Admin, MustChangePassword = false });

        var result = await AuthenticateAsync("Bearer tok");

        Assert.True(result.Succeeded);
        var principal = result.Principal!;
        Assert.Equal("7", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("Admin", principal.FindFirstValue(ClaimTypes.Role));
        Assert.Null(principal.FindFirst(PasswordChangedRequirement.ClaimType));
    }

    [Theory]
    [InlineData("bearer tok")]
    [InlineData("BEARER tok")]
    public async Task HandleAuthenticate_SchemeInDifferentCase_Succeeds(string header)
    {
        _users.Setup(u => u.FindBySessionAsync(AuthManager.HashToken("tok"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 7, Role = Role.Admin });

        var result = await AuthenticateAsync(header);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("Bearer tok", "tok")]
    [InlineData("bearer  tok ", "tok")]
    [InlineData("Basic tok", "")]
    [InlineData("Bearer", "")]
    public void ExtractBearerToken_Header_ReturnsTokenOrEmpty(string header, string expected)
    {
        Assert.Equal(expected, SessionAuthenticationHandler.ExtractBearerToken(header));
    }

    [Fact]
    public async Task HandleAuthenticate_PendingUser_AddsMustChangePasswordClaim()
    {
        _users.Setup(u => u.FindBySessionAsync(AuthManager.HashToken("tok"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 3, Role = Role.Viewer, MustChangePassword = true });

        var result = await AuthenticateAsync("Bearer tok");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal!.FindFirst("must_change_password"));
        Assert.Equal("Viewer", result.Principal.FindFirstValue(ClaimTypes.Role));
    }
}
