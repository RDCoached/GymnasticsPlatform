using Auth.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace GymnasticsPlatform.Api.Authorization;

public static class AuthorizationPolicyExtensions
{
    /// <summary>
    /// Requires the user to have at least one of the specified tenant-scoped roles.
    /// </summary>
    public static AuthorizationPolicyBuilder RequireTenantRole(
        this AuthorizationPolicyBuilder builder,
        params Role[] roles)
    {
        builder.Requirements.Add(new RoleRequirement(roles));
        return builder;
    }
}
