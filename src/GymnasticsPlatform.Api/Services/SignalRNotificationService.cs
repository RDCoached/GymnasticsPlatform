using GymnasticsPlatform.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace GymnasticsPlatform.Api.Services;

public sealed class SignalRNotificationService(IHubContext<NotificationHub> hubContext) : INotificationService
{
    public async Task SendTenantUpdatedNotificationAsync(Guid userId, Guid newTenantId)
    {
        var notification = new
        {
            Type = "TenantUpdated",
            UserId = userId,
            NewTenantId = newTenantId,
            Message = "Your tenant has been updated. Please sign out and sign in again to refresh your session.",
            Timestamp = DateTimeOffset.UtcNow
        };

        await hubContext.Clients
            .Group($"user:{userId}")
            .SendAsync("TenantUpdated", notification);
    }

    public async Task SendNotificationToUserAsync(Guid userId, string message, object? data = null)
    {
        var notification = new
        {
            Message = message,
            Data = data,
            Timestamp = DateTimeOffset.UtcNow
        };

        await hubContext.Clients
            .Group($"user:{userId}")
            .SendAsync("Notification", notification);
    }
}
