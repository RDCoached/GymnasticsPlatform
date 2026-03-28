using Common.Core;
using GymnasticsPlatform.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class TestTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    private Guid? _overrideTenantId;

    public Guid? TenantId
    {
        get
        {
            // First, try to get from HTTP context items (set by middleware during API calls)
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext?.Items.TryGetValue(TenantResolutionMiddleware.TenantIdKey, out var tenantIdObj) == true)
            {
                if (tenantIdObj is Guid tenantId)
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
