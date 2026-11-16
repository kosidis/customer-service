using System.Security.Claims;
using CustomerService.Api.Auth;
using Microsoft.AspNetCore.Authorization;

namespace CustomerService.UnitTests.Auth;

public class PasswordChangedRequirementTests
{
    private static async Task<bool> EvaluateAsync(params Claim[] claims)
    {
        var requirement = new PasswordChangedRequirement();
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Session"));
        var context = new AuthorizationHandlerContext([requirement], user, null);
        await new PasswordChangedHandler().HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task HandleAsync_NoPendingClaim_Succeeds() => Assert.True(await EvaluateAsync());

    [Fact]
    public async Task HandleAsync_PendingClaim_Fails() =>
        Assert.False(await EvaluateAsync(new Claim("must_change_password", "true")));

    [Fact]
    public void AddNamedPolicy_AppendsPasswordChangedRequirement()
    {
        var options = new AuthorizationOptions();
        AuthPolicies.AddNamedPolicy(options, "X", p => p.RequireRole("Admin"));

        Assert.Contains(options.GetPolicy("X")!.Requirements, r => r is PasswordChangedRequirement);
    }

    [Fact]
    public void SignedIn_DoesNotRequirePasswordChange()
    {
        var options = new AuthorizationOptions();
        AuthPolicies.Configure(options);

        Assert.DoesNotContain(options.GetPolicy(AuthPolicies.SignedIn)!.Requirements, r => r is PasswordChangedRequirement);
        Assert.Contains(options.FallbackPolicy!.Requirements, r => r is PasswordChangedRequirement);
        Assert.Contains(options.DefaultPolicy.Requirements, r => r is PasswordChangedRequirement);
    }
}
