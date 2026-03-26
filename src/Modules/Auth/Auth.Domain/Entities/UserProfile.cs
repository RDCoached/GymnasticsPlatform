using Common.Core;

namespace Auth.Domain.Entities;

public sealed class UserProfile : IMultiTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string KeycloakUserId { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    private UserProfile() { }

    public static UserProfile Create(
        Guid tenantId,
        string keycloakUserId,
        string email,
        string fullName,
        DateTimeOffset createdAt)
    {
        return new UserProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            KeycloakUserId = keycloakUserId,
            Email = email,
            FullName = fullName,
            CreatedAt = createdAt,
            LastLoginAt = null
        };
    }

    public void RecordLogin(DateTimeOffset loginTime)
    {
        LastLoginAt = loginTime;
    }
}
