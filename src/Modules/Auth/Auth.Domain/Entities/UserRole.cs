using Common.Core;

namespace Auth.Domain.Entities;

public sealed class UserRole : IMultiTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public string KeycloakUserId { get; private set; } = string.Empty;
    public Role Role { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public string? AssignedBy { get; private set; }

    private UserRole() { }

    public static UserRole Create(
        Guid tenantId,
        string userId,
        Role role,
        string? assignedBy,
        TimeProvider clock)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        return new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            KeycloakUserId = userId,
            Role = role,
            AssignedBy = assignedBy,
            AssignedAt = clock.GetUtcNow()
        };
    }
}
