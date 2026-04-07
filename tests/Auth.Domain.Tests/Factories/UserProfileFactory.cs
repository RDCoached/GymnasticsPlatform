using Auth.Domain.Entities;

namespace Auth.Domain.Tests.Factories;

public static class UserProfileFactory
{
    public static UserProfile Valid(
        Guid? tenantId = null,
        string? providerUserId = null,
        string? email = null,
        string? fullName = null,
        DateTimeOffset? createdAt = null)
    {
        return UserProfile.Create(
            tenantId ?? Guid.NewGuid(),
            providerUserId ?? "keycloak-user-123",
            email ?? "test@example.com",
            fullName ?? "Test User",
            createdAt ?? DateTimeOffset.UtcNow);
    }

    public static UserProfile WithOnboardingPending(
        Guid? tenantId = null,
        string? providerUserId = null)
    {
        var onboardingTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        return Valid(
            tenantId: tenantId ?? onboardingTenantId,
            providerUserId: providerUserId);
    }

    public static UserProfile WithOnboardingCompleted(
        string choice,
        Guid? tenantId = null,
        string? providerUserId = null)
    {
        var userProfile = Valid(tenantId: tenantId, providerUserId: providerUserId);
        userProfile.CompleteOnboarding(choice);
        return userProfile;
    }

    public static UserProfile InTenant(Guid tenantId)
    {
        return Valid(tenantId: tenantId);
    }
}
