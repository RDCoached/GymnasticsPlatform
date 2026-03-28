using Auth.Domain.Entities;

namespace Auth.Domain.Tests.Factories;

public static class UserProfileFactory
{
    public static UserProfile Valid(
        Guid? tenantId = null,
        string? keycloakUserId = null,
        string? email = null,
        string? fullName = null,
        DateTimeOffset? createdAt = null)
    {
        return UserProfile.Create(
            tenantId ?? Guid.NewGuid(),
            keycloakUserId ?? "keycloak-user-123",
            email ?? "test@example.com",
            fullName ?? "Test User",
            createdAt ?? DateTimeOffset.UtcNow);
    }

    public static UserProfile WithOnboardingPending(
        Guid? tenantId = null,
        string? keycloakUserId = null)
    {
        var onboardingTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        return Valid(
            tenantId: tenantId ?? onboardingTenantId,
            keycloakUserId: keycloakUserId);
    }

    public static UserProfile WithOnboardingCompleted(
        string choice,
        Guid? tenantId = null,
        string? keycloakUserId = null)
    {
        var userProfile = Valid(tenantId: tenantId, keycloakUserId: keycloakUserId);
        userProfile.CompleteOnboarding(choice);
        return userProfile;
    }

    public static UserProfile InTenant(Guid tenantId)
    {
        return Valid(tenantId: tenantId);
    }
}
