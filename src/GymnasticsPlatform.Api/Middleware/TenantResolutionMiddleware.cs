using Auth.Application.Services;
using Common.Core.Constants;

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

                if (!tenantId.HasValue)
                {
                    // New user - create profile in onboarding tenant
                    var email = context.User.FindFirst("email")?.Value;
                    var fullName = context.User.FindFirst("name")?.Value;

                    // Set tenant BEFORE creating profile so DbContext can use it
                    context.Items[TenantIdKey] = TenantConstants.OnboardingTenantId;

                    logger.LogInformation("Creating new user profile for {UserId} in onboarding tenant", userId);
                    await userTenantService.UpdateUserTenantAsync(userId, TenantConstants.OnboardingTenantId, email, fullName, context.RequestAborted);
                    tenantId = TenantConstants.OnboardingTenantId;
                }
                else
                {
                    context.Items[TenantIdKey] = tenantId.Value;
                }

                logger.LogDebug("Resolved tenant {TenantId} for user {UserId}", tenantId.Value, userId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resolve tenant for user {UserId}", userId);
            }
        }

        await next(context);
    }
}
