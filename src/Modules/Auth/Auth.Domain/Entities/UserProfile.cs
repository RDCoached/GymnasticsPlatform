using Common.Core;

namespace Auth.Domain.Entities;

public sealed class UserProfile : IMultiTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ProviderUserId { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public bool OnboardingCompleted { get; private set; }
    public string? OnboardingChoice { get; private set; }

    private UserProfile() { }

    public static UserProfile Create(
        Guid tenantId,
        string providerUserId,
        string email,
        string fullName,
        DateTimeOffset createdAt)
    {
        return new UserProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProviderUserId = providerUserId,
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

    public void UpdateProfile(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name cannot be empty.", nameof(fullName));

        if (fullName.Length < 2)
            throw new ArgumentException("Full name must be at least 2 characters.", nameof(fullName));

        if (fullName.Length > 100)
            throw new ArgumentException("Full name must not exceed 100 characters.", nameof(fullName));

        FullName = fullName;
    }
}
