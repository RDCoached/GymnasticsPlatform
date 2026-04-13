using Auth.Domain.Entities;
using Auth.Domain.Events;
using Common.Core.DomainEvents;
using FluentAssertions;

namespace Auth.Domain.Tests;

public sealed class UserProfileTests
{
    [Fact]
    public void Create_WithValidData_ReturnsUserProfileWithCorrectValues()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var providerUserId = "keycloak-user-123";
        var email = "test@example.com";
        var fullName = "Test User";
        var createdAt = DateTimeOffset.UtcNow;

        // Act
        var userProfile = UserProfile.Create(
            tenantId,
            providerUserId,
            email,
            fullName,
            createdAt);

        // Assert
        userProfile.Should().NotBeNull();
        userProfile.Id.Should().NotBeEmpty();
        userProfile.TenantId.Should().Be(tenantId);
        userProfile.ProviderUserId.Should().Be(providerUserId);
        userProfile.Email.Should().Be(email);
        userProfile.FullName.Should().Be(fullName);
        userProfile.CreatedAt.Should().Be(createdAt);
        userProfile.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var providerUserId = "keycloak-user-123";
        var email = "test@example.com";
        var fullName = "Test User";
        var createdAt = DateTimeOffset.UtcNow;

        // Act
        var userProfile1 = UserProfile.Create(tenantId, providerUserId, email, fullName, createdAt);
        var userProfile2 = UserProfile.Create(tenantId, providerUserId, email, fullName, createdAt);

        // Assert
        userProfile1.Id.Should().NotBe(userProfile2.Id);
    }

    [Fact]
    public void RecordLogin_SetsLastLoginAt()
    {
        // Arrange
        var userProfile = UserProfile.Create(
            Guid.NewGuid(),
            "keycloak-user-123",
            "test@example.com",
            "Test User",
            DateTimeOffset.UtcNow);
        var loginTime = DateTimeOffset.UtcNow.AddMinutes(10);

        // Act
        userProfile.RecordLogin(loginTime);

        // Assert
        userProfile.LastLoginAt.Should().Be(loginTime);
    }

    [Fact]
    public void RecordLogin_CanBeCalledMultipleTimes()
    {
        // Arrange
        var userProfile = UserProfile.Create(
            Guid.NewGuid(),
            "keycloak-user-123",
            "test@example.com",
            "Test User",
            DateTimeOffset.UtcNow);
        var firstLogin = DateTimeOffset.UtcNow.AddMinutes(10);
        var secondLogin = DateTimeOffset.UtcNow.AddMinutes(20);

        // Act
        userProfile.RecordLogin(firstLogin);
        userProfile.RecordLogin(secondLogin);

        // Assert
        userProfile.LastLoginAt.Should().Be(secondLogin);
    }

    [Fact]
    public void UserProfile_ImplementsIMultiTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userProfile = UserProfile.Create(
            tenantId,
            "keycloak-user-123",
            "test@example.com",
            "Test User",
            DateTimeOffset.UtcNow);

        // Assert
        userProfile.Should().BeAssignableTo<Common.Core.IMultiTenant>();
        ((Common.Core.IMultiTenant)userProfile).TenantId.Should().Be(tenantId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("simple-id")]
    [InlineData("complex-id-with-dashes-123")]
    public void Create_AcceptsVariousProviderUserIdFormats(string providerUserId)
    {
        // Act
        var userProfile = UserProfile.Create(
            Guid.NewGuid(),
            providerUserId,
            "test@example.com",
            "Test User",
            DateTimeOffset.UtcNow);

        // Assert
        userProfile.ProviderUserId.Should().Be(providerUserId);
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name+tag@example.co.uk")]
    [InlineData("first.last@subdomain.example.com")]
    public void Create_AcceptsVariousEmailFormats(string email)
    {
        // Act
        var userProfile = UserProfile.Create(
            Guid.NewGuid(),
            "keycloak-user-123",
            email,
            "Test User",
            DateTimeOffset.UtcNow);

        // Assert
        userProfile.Email.Should().Be(email);
    }

    [Theory]
    [InlineData("John Doe")]
    [InlineData("María García")]
    [InlineData("李明")]
    [InlineData("O'Brien-Smith")]
    public void Create_AcceptsVariousNameFormats(string fullName)
    {
        // Act
        var userProfile = UserProfile.Create(
            Guid.NewGuid(),
            "keycloak-user-123",
            "test@example.com",
            fullName,
            DateTimeOffset.UtcNow);

        // Assert
        userProfile.FullName.Should().Be(fullName);
    }

    [Fact]
    public void Create_OnboardingCompletedDefaultsToFalse()
    {
        // Act
        var userProfile = UserProfile.Create(
            Guid.NewGuid(),
            "keycloak-user-123",
            "test@example.com",
            "Test User",
            DateTimeOffset.UtcNow);

        // Assert
        userProfile.OnboardingCompleted.Should().BeFalse();
    }

    [Fact]
    public void Create_OnboardingChoiceDefaultsToNull()
    {
        // Act
        var userProfile = UserProfile.Create(
            Guid.NewGuid(),
            "keycloak-user-123",
            "test@example.com",
            "Test User",
            DateTimeOffset.UtcNow);

        // Assert
        userProfile.OnboardingChoice.Should().BeNull();
    }

    [Theory]
    [InlineData("club")]
    [InlineData("individual")]
    public void CompleteOnboarding_SetsCompletedFlagAndChoice(string choice)
    {
        // Arrange
        var userProfile = UserProfile.Create(
            Guid.NewGuid(),
            "keycloak-user-123",
            "test@example.com",
            "Test User",
            DateTimeOffset.UtcNow);

        // Act
        userProfile.CompleteOnboarding(choice);

        // Assert
        userProfile.OnboardingCompleted.Should().BeTrue();
        userProfile.OnboardingChoice.Should().Be(choice);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void CompleteOnboarding_WithInvalidChoice_ThrowsArgumentException(string? invalidChoice)
    {
        // Arrange
        var userProfile = UserProfile.Create(
            Guid.NewGuid(),
            "keycloak-user-123",
            "test@example.com",
            "Test User",
            DateTimeOffset.UtcNow);

        // Act
        var act = () => userProfile.CompleteOnboarding(invalidChoice!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*choice*");
    }

    [Fact]
    public void CompleteOnboarding_CannotBeCalledTwice()
    {
        // Arrange
        var userProfile = UserProfile.Create(
            Guid.NewGuid(),
            "keycloak-user-123",
            "test@example.com",
            "Test User",
            DateTimeOffset.UtcNow);
        userProfile.CompleteOnboarding("club");

        // Act
        var act = () => userProfile.CompleteOnboarding("individual");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already*");
    }

    [Fact]
    public void UpdateTenant_RaisesDomainEvent_WithCorrectValues()
    {
        // Arrange
        var oldTenantId = Guid.NewGuid();
        var newTenantId = Guid.NewGuid();
        var providerUserId = "entra-user-123";
        var email = "test@example.com";
        var fullName = "Test User";
        var clock = TimeProvider.System;
        var userProfile = UserProfile.Create(
            oldTenantId,
            providerUserId,
            email,
            fullName,
            DateTimeOffset.UtcNow);

        // Act
        userProfile.UpdateTenant(newTenantId, clock);

        // Assert
        userProfile.TenantId.Should().Be(newTenantId);
        userProfile.DomainEvents.Should().HaveCount(1);
        var domainEvent = userProfile.DomainEvents.Single();
        domainEvent.Should().BeOfType<UserTenantUpdatedEvent>();
        var tenantUpdatedEvent = (UserTenantUpdatedEvent)domainEvent;
        tenantUpdatedEvent.UserId.Should().Be(userProfile.Id);
        tenantUpdatedEvent.ProviderUserId.Should().Be(providerUserId);
        tenantUpdatedEvent.OldTenantId.Should().Be(oldTenantId);
        tenantUpdatedEvent.NewTenantId.Should().Be(newTenantId);
        tenantUpdatedEvent.Email.Should().Be(email);
        tenantUpdatedEvent.FullName.Should().Be(fullName);
        tenantUpdatedEvent.OccurredAt.Should().BeCloseTo(clock.GetUtcNow(), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateTenant_WithEmptyTenantId_ThrowsArgumentException_AndDoesNotRaiseEvent()
    {
        // Arrange
        var userProfile = UserProfile.Create(
            Guid.NewGuid(),
            "entra-user-123",
            "test@example.com",
            "Test User",
            DateTimeOffset.UtcNow);
        var clock = TimeProvider.System;

        // Act
        var act = () => userProfile.UpdateTenant(Guid.Empty, clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Tenant ID*");
        userProfile.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UserProfile_ImplementsIHasDomainEvents()
    {
        // Arrange
        var userProfile = UserProfile.Create(
            Guid.NewGuid(),
            "entra-user-123",
            "test@example.com",
            "Test User",
            DateTimeOffset.UtcNow);

        // Assert
        userProfile.Should().BeAssignableTo<IHasDomainEvents>();
        userProfile.DomainEvents.Should().NotBeNull();
        userProfile.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        // Arrange
        var oldTenantId = Guid.NewGuid();
        var newTenantId = Guid.NewGuid();
        var clock = TimeProvider.System;
        var userProfile = UserProfile.Create(
            oldTenantId,
            "entra-user-123",
            "test@example.com",
            "Test User",
            DateTimeOffset.UtcNow);
        userProfile.UpdateTenant(newTenantId, clock);
        userProfile.DomainEvents.Should().HaveCount(1);

        // Act
        userProfile.ClearDomainEvents();

        // Assert
        userProfile.DomainEvents.Should().BeEmpty();
    }
}
