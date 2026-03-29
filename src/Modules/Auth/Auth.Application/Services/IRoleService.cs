using Auth.Domain.Entities;

namespace Auth.Application.Services;

/// <summary>
/// Service for managing tenant-scoped user roles.
/// </summary>
public interface IRoleService
{
    /// <summary>
    /// Assigns one or more roles to a user in a tenant.
    /// This operation is idempotent - duplicate roles are ignored.
    /// </summary>
    Task AssignRolesAsync(
        Guid tenantId,
        string keycloakUserId,
        IReadOnlyList<Role> roles,
        string? assignedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all roles for a user in a specific tenant.
    /// </summary>
    Task<IReadOnlyList<Role>> GetUserRolesAsync(
        Guid tenantId,
        string keycloakUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a user has a specific role in a tenant.
    /// </summary>
    Task<bool> HasRoleAsync(
        Guid tenantId,
        string keycloakUserId,
        Role role,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a user has any of the specified roles in a tenant.
    /// </summary>
    Task<bool> HasAnyRoleAsync(
        Guid tenantId,
        string keycloakUserId,
        IReadOnlyList<Role> roles,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a specific role from a user in a tenant.
    /// </summary>
    Task RemoveRoleAsync(
        Guid tenantId,
        string keycloakUserId,
        Role role,
        CancellationToken ct = default);
}
