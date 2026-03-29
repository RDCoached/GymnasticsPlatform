using Auth.Application.Services;
using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using Auth.Infrastructure.Services;
using Common.Core;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Auth.Infrastructure.Tests.Services;

[Collection("Database collection")]
public sealed class RoleServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;

    public RoleServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private string ConnectionString => _fixture.ConnectionString;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var connection = new Npgsql.NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM user_roles; DELETE FROM user_profiles; DELETE FROM club_invites; DELETE FROM clubs";
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task AssignRolesAsync_NewRole_AddsRoleToDatabase()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var roles = new List<Role> { Role.Coach }.AsReadOnly();
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        // Act
        await service.AssignRolesAsync(tenantId, userId, roles, null, CancellationToken.None);

        // Assert
        var userRoles = await db.UserRoles.Where(ur => ur.KeycloakUserId == userId).ToListAsync();
        userRoles.Should().HaveCount(1);
        userRoles[0].TenantId.Should().Be(tenantId);
        userRoles[0].Role.Should().Be(Role.Coach);
        userRoles[0].AssignedBy.Should().BeNull();
    }

    [Fact]
    public async Task AssignRolesAsync_MultipleRoles_AddsAllRolesToDatabase()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var roles = new List<Role> { Role.ClubAdmin, Role.Coach }.AsReadOnly();
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        // Act
        await service.AssignRolesAsync(tenantId, userId, roles, null, CancellationToken.None);

        // Assert
        var userRoles = await db.UserRoles
            .Where(ur => ur.KeycloakUserId == userId)
            .OrderBy(ur => ur.Role)
            .ToListAsync();
        userRoles.Should().HaveCount(2);
        userRoles[0].Role.Should().Be(Role.ClubAdmin);
        userRoles[1].Role.Should().Be(Role.Coach);
    }

    [Fact]
    public async Task AssignRolesAsync_DuplicateRole_DoesNotDuplicate()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var roles = new List<Role> { Role.Coach }.AsReadOnly();
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        // Act - Assign the same role twice
        await service.AssignRolesAsync(tenantId, userId, roles, null, CancellationToken.None);
        await service.AssignRolesAsync(tenantId, userId, roles, null, CancellationToken.None);

        // Assert - Should only have one role entry
        var userRoles = await db.UserRoles.Where(ur => ur.KeycloakUserId == userId).ToListAsync();
        userRoles.Should().HaveCount(1);
    }

    [Fact]
    public async Task AssignRolesAsync_WithAssignedBy_StoresAssignedBy()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var assignedBy = "admin-user-456";
        var roles = new List<Role> { Role.Gymnast }.AsReadOnly();
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        // Act
        await service.AssignRolesAsync(tenantId, userId, roles, assignedBy, CancellationToken.None);

        // Assert
        var userRole = await db.UserRoles.FirstAsync(ur => ur.KeycloakUserId == userId);
        userRole.AssignedBy.Should().Be(assignedBy);
    }

    [Fact]
    public async Task GetUserRolesAsync_UserHasRoles_ReturnsRoles()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var roles = new List<Role> { Role.ClubAdmin, Role.Coach }.AsReadOnly();
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        await service.AssignRolesAsync(tenantId, userId, roles, null, CancellationToken.None);

        // Act
        var userRoles = await service.GetUserRolesAsync(tenantId, userId, CancellationToken.None);

        // Assert
        userRoles.Should().HaveCount(2);
        userRoles.Should().Contain(Role.ClubAdmin);
        userRoles.Should().Contain(Role.Coach);
    }

    [Fact]
    public async Task GetUserRolesAsync_UserHasNoRoles_ReturnsEmptyList()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        // Act
        var userRoles = await service.GetUserRolesAsync(tenantId, userId, CancellationToken.None);

        // Assert
        userRoles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_TenantScoped_OnlyReturnsRolesForTenant()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var clock = TimeProvider.System;

        // Assign roles in tenant 1
        var tenant1Context = CreateTenantContext(tenant1Id);
        await using (var db = CreateDbContext(tenant1Context))
        {
            var service = new RoleService(db, clock);
            await service.AssignRolesAsync(tenant1Id, userId, new List<Role> { Role.ClubAdmin }.AsReadOnly(), null, CancellationToken.None);
        }

        // Assign roles in tenant 2
        var tenant2Context = CreateTenantContext(tenant2Id);
        await using (var db = CreateDbContext(tenant2Context))
        {
            var service = new RoleService(db, clock);
            await service.AssignRolesAsync(tenant2Id, userId, new List<Role> { Role.Gymnast }.AsReadOnly(), null, CancellationToken.None);
        }

        // Act & Assert - Tenant 1 context should only see tenant 1 roles
        await using (var db = CreateDbContext(tenant1Context))
        {
            var service = new RoleService(db, clock);
            var userRoles = await service.GetUserRolesAsync(tenant1Id, userId, CancellationToken.None);
            userRoles.Should().HaveCount(1);
            userRoles.Should().Contain(Role.ClubAdmin);
        }

        // Act & Assert - Tenant 2 context should only see tenant 2 roles
        await using (var db = CreateDbContext(tenant2Context))
        {
            var service = new RoleService(db, clock);
            var userRoles = await service.GetUserRolesAsync(tenant2Id, userId, CancellationToken.None);
            userRoles.Should().HaveCount(1);
            userRoles.Should().Contain(Role.Gymnast);
        }
    }

    [Fact]
    public async Task HasRoleAsync_UserHasRole_ReturnsTrue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var roles = new List<Role> { Role.Coach }.AsReadOnly();
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        await service.AssignRolesAsync(tenantId, userId, roles, null, CancellationToken.None);

        // Act
        var hasRole = await service.HasRoleAsync(tenantId, userId, Role.Coach, CancellationToken.None);

        // Assert
        hasRole.Should().BeTrue();
    }

    [Fact]
    public async Task HasRoleAsync_UserDoesNotHaveRole_ReturnsFalse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        // Act
        var hasRole = await service.HasRoleAsync(tenantId, userId, Role.ClubAdmin, CancellationToken.None);

        // Assert
        hasRole.Should().BeFalse();
    }

    [Fact]
    public async Task HasAnyRoleAsync_UserHasOneOfMultiple_ReturnsTrue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var assignedRoles = new List<Role> { Role.Coach }.AsReadOnly();
        var checkRoles = new List<Role> { Role.ClubAdmin, Role.Coach, Role.Gymnast }.AsReadOnly();
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        await service.AssignRolesAsync(tenantId, userId, assignedRoles, null, CancellationToken.None);

        // Act
        var hasAnyRole = await service.HasAnyRoleAsync(tenantId, userId, checkRoles, CancellationToken.None);

        // Assert
        hasAnyRole.Should().BeTrue();
    }

    [Fact]
    public async Task HasAnyRoleAsync_UserHasNone_ReturnsFalse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var checkRoles = new List<Role> { Role.ClubAdmin, Role.Gymnast }.AsReadOnly();
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        // Act
        var hasAnyRole = await service.HasAnyRoleAsync(tenantId, userId, checkRoles, CancellationToken.None);

        // Assert
        hasAnyRole.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveRoleAsync_ExistingRole_RemovesFromDatabase()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var roles = new List<Role> { Role.ClubAdmin, Role.Coach }.AsReadOnly();
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        await service.AssignRolesAsync(tenantId, userId, roles, null, CancellationToken.None);

        // Act
        await service.RemoveRoleAsync(tenantId, userId, Role.ClubAdmin, CancellationToken.None);

        // Assert
        var userRoles = await service.GetUserRolesAsync(tenantId, userId, CancellationToken.None);
        userRoles.Should().HaveCount(1);
        userRoles.Should().Contain(Role.Coach);
        userRoles.Should().NotContain(Role.ClubAdmin);
    }

    [Fact]
    public async Task RemoveRoleAsync_NonExistentRole_DoesNotThrow()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        // Act
        var act = async () => await service.RemoveRoleAsync(tenantId, userId, Role.ClubAdmin, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    private AuthDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AuthDbContext(options, tenantContext);
    }

    private static ITenantContext CreateTenantContext(Guid? tenantId)
    {
        var context = Substitute.For<ITenantContext>();
        context.TenantId.Returns(tenantId);
        return context;
    }
}
