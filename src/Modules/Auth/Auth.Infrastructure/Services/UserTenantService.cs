using Auth.Application.Services;
using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Auth.Infrastructure.Services;

public sealed class UserTenantService : IUserTenantService
{
    private readonly AuthDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ILogger<UserTenantService> _logger;
    private readonly IKeycloakAdminService _keycloakService;

    public UserTenantService(
        AuthDbContext db,
        TimeProvider clock,
        ILogger<UserTenantService> logger,
        IKeycloakAdminService keycloakService)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
        _keycloakService = keycloakService;
    }

    public async Task<Guid?> GetUserTenantIdAsync(string providerUserId, CancellationToken ct = default)
    {
        // Query without tenant filter since we're looking up the tenant itself
        var userProfile = await _db.UserProfiles
            .IgnoreQueryFilters()
            .Where(u => u.ProviderUserId == providerUserId)
            .Select(u => new { u.TenantId })
            .FirstOrDefaultAsync(ct);

        return userProfile?.TenantId;
    }

    public async Task UpdateUserTenantAsync(
        string providerUserId,
        Guid newTenantId,
        string? email = null,
        string? fullName = null,
        CancellationToken ct = default)
    {
        // Query without tenant filter to find user across all tenants
        var userProfile = await _db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.ProviderUserId == providerUserId, ct);

        if (userProfile is not null)
        {
            // Update existing profile's tenant
            var oldTenantId = userProfile.TenantId;
            userProfile.UpdateTenant(newTenantId);

            _logger.LogInformation(
                "Updated user {UserId} tenant from {OldTenant} to {NewTenant}",
                providerUserId, oldTenantId, newTenantId);
        }
        else
        {
            // Create new profile if it doesn't exist
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(fullName))
            {
                throw new InvalidOperationException(
                    "Email and full name are required when creating a new user profile");
            }

            userProfile = UserProfile.Create(
                newTenantId,
                providerUserId,
                email,
                fullName,
                _clock.GetUtcNow());

            _db.UserProfiles.Add(userProfile);

            _logger.LogInformation(
                "Created user profile for {UserId} in tenant {TenantId}",
                providerUserId, newTenantId);
        }

        await _db.SaveChangesAsync(ct);

        // Update Keycloak user attributes so future JWT tokens have correct tenant_id
        await _keycloakService.UpdateUserTenantIdAsync(providerUserId, newTenantId, ct);
    }
}
