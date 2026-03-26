using System.Security.Claims;
using Common.Core;

namespace GymnasticsPlatform.Api.Services;

public sealed class TenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    private const string TenantIdClaimType = "tenant_id";

    public Guid? TenantId
    {
        get
        {
            var tenantIdClaim = httpContextAccessor.HttpContext?.User
                .FindFirst(TenantIdClaimType)?.Value;

            return tenantIdClaim is not null && Guid.TryParse(tenantIdClaim, out var tenantId)
                ? tenantId
                : null;
        }
    }
}
