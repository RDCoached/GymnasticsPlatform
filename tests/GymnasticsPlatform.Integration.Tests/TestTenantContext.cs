using Common.Core;
using Microsoft.AspNetCore.Http;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class TestTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    private const string TenantIdClaimType = "tenant_id";
    private Guid? _overrideTenantId;

    public Guid? TenantId
    {
        get
        {
            // First, try to get from HTTP context (for API calls)
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext is not null)
            {
                var tenantIdClaim = httpContext.User.FindFirst(TenantIdClaimType)?.Value;
                if (tenantIdClaim is not null && Guid.TryParse(tenantIdClaim, out var tenantId))
                {
                    return tenantId;
                }
            }

            // Fallback to override value (for direct DB operations in tests)
            return _overrideTenantId;
        }
        set => _overrideTenantId = value;
    }
}
