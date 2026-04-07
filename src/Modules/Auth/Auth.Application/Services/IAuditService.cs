namespace Auth.Application.Services;

public interface IAuditService
{
    Task LogActionAsync(
        Guid performedByUserId,
        string action,
        string entityType,
        string entityId,
        string? oldValue = null,
        string? newValue = null,
        CancellationToken ct = default);
}
