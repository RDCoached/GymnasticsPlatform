using Auth.Application.Services;
using Common.Core;
using Microsoft.AspNetCore.Authorization;

namespace GymnasticsPlatform.Api.Authorization;

public sealed class TenantRoleAuthorizationHandler : AuthorizationHandler<RoleRequirement>
{
    private readonly IRoleService _roleService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<TenantRoleAuthorizationHandler> _logger;

    public TenantRoleAuthorizationHandler(
        IRoleService roleService,
        ITenantContext tenantContext,
        ILogger<TenantRoleAuthorizationHandler> logger)
    {
        _roleService = roleService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RoleRequirement requirement)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Authorization failed: No user ID found in claims");
            context.Fail();
            return;
        }

        var tenantId = _tenantContext.TenantId;
        if (tenantId is null)
        {
            _logger.LogWarning("Authorization failed: No tenant context available for user {UserId}", userId);
            context.Fail();
            return;
        }

        try
        {
            var hasAnyRole = await _roleService.HasAnyRoleAsync(
                tenantId.Value,
                userId,
                requirement.Roles,
                CancellationToken.None);

            if (hasAnyRole)
            {
                _logger.LogDebug(
                    "Authorization succeeded: User {UserId} has one of required roles in tenant {TenantId}",
                    userId,
                    tenantId);
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning(
                    "Authorization failed: User {UserId} does not have any of required roles {Roles} in tenant {TenantId}",
                    userId,
                    string.Join(", ", requirement.Roles),
                    tenantId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Authorization failed: Error checking roles for user {UserId} in tenant {TenantId}",
                userId,
                tenantId);
            context.Fail();
        }
    }
}
