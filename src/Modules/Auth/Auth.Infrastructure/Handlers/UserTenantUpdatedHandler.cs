using Auth.Application.Services;
using Auth.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Auth.Infrastructure.Handlers;

public sealed class UserTenantUpdatedHandler(
    IAuthenticationProvider authProvider,
    ILogger<UserTenantUpdatedHandler> logger)
{
    public async Task HandleAsync(UserTenantUpdatedEvent evt, CancellationToken ct)
    {
        logger.LogInformation(
            "Processing UserTenantUpdatedEvent: User {UserId} tenant changed from {OldTenantId} to {NewTenantId}",
            evt.UserId,
            evt.OldTenantId,
            evt.NewTenantId);

        try
        {
            await authProvider.UpdateUserTenantIdAsync(evt.ProviderUserId, evt.NewTenantId, ct);

            logger.LogInformation(
                "Successfully processed tenant update in external provider for user {UserId} (Provider: {ProviderUserId})",
                evt.UserId,
                evt.ProviderUserId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to update tenant ID in external provider for user {UserId} (Provider: {ProviderUserId})",
                evt.UserId,
                evt.ProviderUserId);
            throw;
        }
    }
}
