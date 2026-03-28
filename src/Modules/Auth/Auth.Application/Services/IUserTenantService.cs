namespace Auth.Application.Services;

/// <summary>
/// Service for looking up a user's current tenant ID from the database.
/// </summary>
public interface IUserTenantService
{
    /// <summary>
    /// Gets the tenant ID for a user by their Keycloak user ID.
    /// Returns null if the user doesn't exist or has no tenant assigned.
    /// </summary>
    Task<Guid?> GetUserTenantIdAsync(string keycloakUserId, CancellationToken ct = default);

    /// <summary>
    /// Updates the user's tenant ID in the database.
    /// Creates a user profile if one doesn't exist.
    /// </summary>
    Task UpdateUserTenantAsync(string keycloakUserId, Guid newTenantId, string? email = null, string? fullName = null, CancellationToken ct = default);
}
