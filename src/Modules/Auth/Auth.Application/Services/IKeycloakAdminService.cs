namespace Auth.Application.Services;

public interface IKeycloakAdminService
{
    Task UpdateUserTenantIdAsync(string keycloakUserId, Guid newTenantId, CancellationToken ct = default);
}
