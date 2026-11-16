using Microsoft.AspNetCore.Authorization;

namespace CustomerService.Api.Auth;

/// <summary>Fails while the signed-in user still has to replace a temporary password.</summary>
public class PasswordChangedRequirement : IAuthorizationRequirement
{
    public const string ClaimType = "must_change_password";
}

public class PasswordChangedHandler : AuthorizationHandler<PasswordChangedRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PasswordChangedRequirement requirement)
    {
        if (!context.User.HasClaim(c => c.Type == PasswordChangedRequirement.ClaimType))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
