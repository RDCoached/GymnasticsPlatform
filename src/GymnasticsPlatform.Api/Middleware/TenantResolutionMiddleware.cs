using Auth.Application.Services;

namespace GymnasticsPlatform.Api.Middleware;

public sealed class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public const string TenantIdKey = "TenantId";

    public async Task InvokeAsync(HttpContext context, IUserTenantService userTenantService)
    {
        var userId = context.User.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            try
            {
                var tenantId = await userTenantService.GetUserTenantIdAsync(userId, context.RequestAborted);
                if (tenantId.HasValue)
                {
                    context.Items[TenantIdKey] = tenantId.Value;
                    logger.LogDebug("Resolved tenant {TenantId} for user {UserId}", tenantId.Value, userId);
                }
                else
                {
                    logger.LogWarning("No tenant found for user {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resolve tenant for user {UserId}", userId);
            }
        }

        await next(context);
    }
}
