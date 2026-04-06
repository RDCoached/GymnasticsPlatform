using Auth.Application.Services;
using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;

namespace Auth.Infrastructure.Services;

public sealed class AuditService(
    AuthDbContext db,
    TimeProvider clock) : IAuditService
{
    public async Task LogActionAsync(
        Guid performedByUserId,
        string action,
        string entityType,
        string entityId,
        string? oldValue = null,
        string? newValue = null,
        CancellationToken ct = default)
    {
        var auditLog = AuditLog.Create(
            performedByUserId: performedByUserId,
            action: action,
            entityType: entityType,
            entityId: entityId,
            oldValue: oldValue,
            newValue: newValue,
            performedAt: clock.GetUtcNow(),
            ipAddress: null,
            userAgent: null);

        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync(ct);
    }
}
