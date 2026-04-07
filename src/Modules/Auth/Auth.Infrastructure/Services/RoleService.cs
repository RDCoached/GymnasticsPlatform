using Auth.Application.Services;
using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Auth.Infrastructure.Services;

public sealed class RoleService : IRoleService
{
    private readonly AuthDbContext _db;
    private readonly TimeProvider _clock;

    public RoleService(AuthDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task AssignRolesAsync(
        Guid tenantId,
        string providerUserId,
        IReadOnlyList<Role> roles,
        string? assignedBy,
        CancellationToken ct = default)
    {
        var existingRoles = await _db.UserRoles
            .IgnoreQueryFilters()
            .Where(ur => ur.TenantId == tenantId && ur.ProviderUserId == providerUserId)
            .Select(ur => ur.Role)
            .ToListAsync(ct);

        var rolesToAdd = roles.Except(existingRoles).ToList();

        foreach (var role in rolesToAdd)
        {
            var userRole = UserRole.Create(tenantId, providerUserId, role, assignedBy, _clock);
            _db.UserRoles.Add(userRole);
        }

        if (rolesToAdd.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<Role>> GetUserRolesAsync(
        Guid tenantId,
        string providerUserId,
        CancellationToken ct = default)
    {
        var roles = await _db.UserRoles
            .IgnoreQueryFilters()
            .Where(ur => ur.TenantId == tenantId && ur.ProviderUserId == providerUserId)
            .Select(ur => ur.Role)
            .ToListAsync(ct);

        return roles.AsReadOnly();
    }

    public async Task<bool> HasRoleAsync(
        Guid tenantId,
        string providerUserId,
        Role role,
        CancellationToken ct = default)
    {
        return await _db.UserRoles
            .IgnoreQueryFilters()
            .AnyAsync(ur => ur.TenantId == tenantId && ur.ProviderUserId == providerUserId && ur.Role == role, ct);
    }

    public async Task<bool> HasAnyRoleAsync(
        Guid tenantId,
        string providerUserId,
        IReadOnlyList<Role> roles,
        CancellationToken ct = default)
    {
        return await _db.UserRoles
            .IgnoreQueryFilters()
            .AnyAsync(ur => ur.TenantId == tenantId && ur.ProviderUserId == providerUserId && roles.Contains(ur.Role), ct);
    }

    public async Task RemoveRoleAsync(
        Guid tenantId,
        string providerUserId,
        Role role,
        CancellationToken ct = default)
    {
        var userRole = await _db.UserRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ur => ur.TenantId == tenantId && ur.ProviderUserId == providerUserId && ur.Role == role, ct);

        if (userRole is not null)
        {
            _db.UserRoles.Remove(userRole);
            await _db.SaveChangesAsync(ct);
        }
    }
}
