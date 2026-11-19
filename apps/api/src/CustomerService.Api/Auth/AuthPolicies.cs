using CustomerService.Api.Domain;
using Microsoft.AspNetCore.Authorization;

namespace CustomerService.Api.Auth;

public static class AuthPolicies
{
    /// <summary>Any authenticated user, even one who must still change a temporary password.</summary>
    public const string SignedIn = "SignedIn";

    /// <summary>Admins who have already changed their temporary password; guards customer writes.</summary>
    public const string AdminOnly = "AdminOnly";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(SignedIn, policy => policy.RequireAuthenticatedUser());

        AddNamedPolicy(options, AdminOnly, policy => policy.RequireRole(nameof(Role.Admin)));

        var defaultPolicy = Build(policy => policy.RequireAuthenticatedUser());
        options.DefaultPolicy = defaultPolicy;
        options.FallbackPolicy = defaultPolicy;
    }

    /// <summary>Adds a policy that always includes <see cref="PasswordChangedRequirement"/>.</summary>
    public static void AddNamedPolicy(
        AuthorizationOptions options, string name, Action<AuthorizationPolicyBuilder> configure) =>
        options.AddPolicy(name, Build(configure));

    private static AuthorizationPolicy Build(Action<AuthorizationPolicyBuilder> configure)
    {
        var builder = new AuthorizationPolicyBuilder();
        configure(builder);
        builder.AddRequirements(new PasswordChangedRequirement());
        return builder.Build();
    }
}
