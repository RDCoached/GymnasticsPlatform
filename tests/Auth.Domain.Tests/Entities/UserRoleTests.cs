using Auth.Domain.Entities;
using Common.Core;
using FluentAssertions;

namespace Auth.Domain.Tests.Entities;

public sealed class UserRoleTests
{
    [Fact]
    public void Create_WithValidParameters_ReturnsUserRole()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var role = Role.Coach;
        var assignedBy = "admin-user-456";
        var clock = TimeProvider.System;

        // Act
        var userRole = UserRole.Create(tenantId, userId, role, assignedBy, clock);

        // Assert
        userRole.Should().NotBeNull();
        userRole.Id.Should().NotBeEmpty();
        userRole.TenantId.Should().Be(tenantId);
        userRole.KeycloakUserId.Should().Be(userId);
        userRole.Role.Should().Be(role);
        userRole.AssignedBy.Should().Be(assignedBy);
        userRole.AssignedAt.Should().BeCloseTo(clock.GetUtcNow(), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithNullAssignedBy_ReturnsUserRoleWithNullAssignedBy()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var role = Role.ClubAdmin;
        var clock = TimeProvider.System;

        // Act
        var userRole = UserRole.Create(tenantId, userId, role, null, clock);

        // Assert
        userRole.AssignedBy.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithEmptyUserId_ThrowsArgumentException(string? invalidUserId)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var role = Role.Coach;
        var clock = TimeProvider.System;

        // Act
        var act = () => UserRole.Create(tenantId, invalidUserId!, role, null, clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*userId*");
    }

    [Fact]
    public void Create_WithEmptyTenantId_ThrowsArgumentException()
    {
        // Arrange
        var userId = "keycloak-user-123";
        var role = Role.Gymnast;
        var clock = TimeProvider.System;

        // Act
        var act = () => UserRole.Create(Guid.Empty, userId, role, null, clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*tenantId*");
    }

    [Fact]
    public void UserRole_ImplementsIMultiTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userRole = UserRole.Create(tenantId, "user-123", Role.Coach, null, TimeProvider.System);

        // Assert
        userRole.Should().BeAssignableTo<IMultiTenant>();
        ((IMultiTenant)userRole).TenantId.Should().Be(tenantId);
    }

    [Theory]
    [InlineData(Role.ClubAdmin)]
    [InlineData(Role.Coach)]
    [InlineData(Role.Gymnast)]
    [InlineData(Role.IndividualAdmin)]
    public void Create_AcceptsAllRoleTypes(Role role)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "user-123";
        var clock = TimeProvider.System;

        // Act
        var userRole = UserRole.Create(tenantId, userId, role, null, clock);

        // Assert
        userRole.Role.Should().Be(role);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "user-123";
        var role = Role.Coach;
        var clock = TimeProvider.System;

        // Act
        var userRole1 = UserRole.Create(tenantId, userId, role, null, clock);
        var userRole2 = UserRole.Create(tenantId, userId, role, null, clock);

        // Assert
        userRole1.Id.Should().NotBe(userRole2.Id);
    }
}
