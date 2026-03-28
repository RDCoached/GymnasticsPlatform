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
    public bool OnboardingCompleted { get; private set; }
    public string? OnboardingChoice { get; private set; }

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
            LastLoginAt = null,
            OnboardingCompleted = false,
            OnboardingChoice = null
        };
    }

    public void RecordLogin(DateTimeOffset loginTime)
    {
        LastLoginAt = loginTime;
    }

    public void CompleteOnboarding(string choice)
    {
        if (string.IsNullOrWhiteSpace(choice))
            throw new ArgumentException("Onboarding choice cannot be empty.", nameof(choice));

        if (OnboardingCompleted)
            throw new InvalidOperationException("Onboarding has already been completed.");

        OnboardingCompleted = true;
        OnboardingChoice = choice;
    }

    public void UpdateTenant(Guid newTenantId)
    {
        if (newTenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(newTenantId));

        TenantId = newTenantId;
    }
}
