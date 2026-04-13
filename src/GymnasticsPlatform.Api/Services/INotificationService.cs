namespace GymnasticsPlatform.Api.Services;

public interface INotificationService
{
    Task SendTenantUpdatedNotificationAsync(Guid userId, Guid newTenantId);
    Task SendNotificationToUserAsync(Guid userId, string message, object? data = null);
}
