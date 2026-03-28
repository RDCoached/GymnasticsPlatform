using Common.Core;
using GymnasticsPlatform.Api.Middleware;

namespace GymnasticsPlatform.Api.Services;

public sealed class TenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid? TenantId
    {
        get
        {
            return httpContextAccessor.HttpContext?.Items[TenantResolutionMiddleware.TenantIdKey] as Guid?;
        }
    }
}
