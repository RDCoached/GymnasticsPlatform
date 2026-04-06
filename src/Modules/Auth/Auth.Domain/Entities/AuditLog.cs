namespace Auth.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public required Guid PerformedByUserId { get; init; }
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public required DateTimeOffset PerformedAt { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }

    private AuditLog() { }

    public static AuditLog Create(
        Guid performedByUserId,
        string action,
        string entityType,
        string entityId,
        string? oldValue,
        string? newValue,
        DateTimeOffset performedAt,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            PerformedByUserId = performedByUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = oldValue,
            NewValue = newValue,
            PerformedAt = performedAt,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
    }
}
