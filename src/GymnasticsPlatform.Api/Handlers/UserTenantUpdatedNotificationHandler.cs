using Auth.Domain.Events;
using GymnasticsPlatform.Api.Services;
using Microsoft.Extensions.Logging;

namespace GymnasticsPlatform.Api.Handlers;

public sealed class UserTenantUpdatedNotificationHandler(
    INotificationService notificationService,
    ILogger<UserTenantUpdatedNotificationHandler> logger)
{
    public async Task HandleAsync(UserTenantUpdatedEvent evt, CancellationToken ct)
    {
        logger.LogInformation(
            "Sending SignalR notification for tenant update: User {UserId} moved to tenant {NewTenantId}",
            evt.UserId,
            evt.NewTenantId);

        try
        {
            await notificationService.SendTenantUpdatedNotificationAsync(evt.UserId, evt.NewTenantId);

            logger.LogInformation(
                "Successfully sent tenant update notification to user {UserId}",
                evt.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send SignalR notification for user {UserId}",
                evt.UserId);
            // Don't rethrow - notification failure shouldn't break the domain event
        }
    }
}
