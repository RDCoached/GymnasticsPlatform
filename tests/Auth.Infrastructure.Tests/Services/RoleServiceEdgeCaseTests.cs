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
public sealed class RoleServiceEdgeCaseTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;

    public RoleServiceEdgeCaseTests(DatabaseFixture fixture)
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task AssignRolesAsync_EmptyUserId_ThrowsArgumentException(string? invalidUserId)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var roles = new List<Role> { Role.Coach }.AsReadOnly();
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        // Act
        var act = async () => await service.AssignRolesAsync(tenantId, invalidUserId!, roles, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*User ID*");
    }

    [Fact]
    public async Task AssignRolesAsync_EmptyTenantId_ThrowsArgumentException()
    {
        // Arrange
        var tenantId = Guid.Empty;
        var userId = "keycloak-user-123";
        var roles = new List<Role> { Role.Coach }.AsReadOnly();
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        // Act
        var act = async () => await service.AssignRolesAsync(tenantId, userId, roles, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Tenant ID*");
    }

    [Fact]
    public async Task AssignRolesAsync_EmptyRolesList_DoesNothing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var roles = new List<Role>().AsReadOnly();
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        // Act
        await service.AssignRolesAsync(tenantId, userId, roles, null, CancellationToken.None);

        // Assert
        var userRoles = await db.UserRoles.ToListAsync();
        userRoles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_WithEmptyUserId_ReturnsEmptyList()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = string.Empty;
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
    public async Task GetUserRolesAsync_WithEmptyTenantId_ReturnsEmptyList()
    {
        // Arrange
        var tenantId = Guid.Empty;
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
    public async Task HasRoleAsync_WithEmptyUserId_ReturnsFalse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = string.Empty;
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        // Act
        var hasRole = await service.HasRoleAsync(tenantId, userId, Role.Coach, CancellationToken.None);

        // Assert
        hasRole.Should().BeFalse();
    }

    [Fact]
    public async Task HasAnyRoleAsync_WithEmptyRolesList_ReturnsFalse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var checkRoles = new List<Role>().AsReadOnly();
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
    public async Task RemoveRoleAsync_WithEmptyUserId_DoesNotThrow()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = string.Empty;
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        // Act
        var act = async () => await service.RemoveRoleAsync(tenantId, userId, Role.Coach, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveRoleAsync_WithEmptyTenantId_DoesNotThrow()
    {
        // Arrange
        var tenantId = Guid.Empty;
        var userId = "keycloak-user-123";
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        var service = new RoleService(db, clock);

        // Act
        var act = async () => await service.RemoveRoleAsync(tenantId, userId, Role.Coach, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AssignRolesAsync_WithAllRoleTypes_AddsAllSuccessfully()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var roles = new List<Role>
        {
            Role.Gymnast,
            Role.Coach,
            Role.ClubAdmin,
            Role.IndividualAdmin
        }.AsReadOnly();
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

        userRoles.Should().HaveCount(4);
        userRoles.Select(ur => ur.Role).Should().BeEquivalentTo(new[]
        {
            Role.Gymnast,
            Role.Coach,
            Role.ClubAdmin,
            Role.IndividualAdmin
        });
    }

    [Fact]
    public async Task AssignRolesAsync_ConcurrentAssignments_HandlesCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);

        // Act - Assign different roles concurrently
        var tasks = new[]
        {
            Task.Run(async () =>
            {
                await using var db = CreateDbContext(tenantContext);
                var service = new RoleService(db, clock);
                await service.AssignRolesAsync(tenantId, userId, new List<Role> { Role.Coach }.AsReadOnly(), null, CancellationToken.None);
            }),
            Task.Run(async () =>
            {
                await using var db = CreateDbContext(tenantContext);
                var service = new RoleService(db, clock);
                await service.AssignRolesAsync(tenantId, userId, new List<Role> { Role.ClubAdmin }.AsReadOnly(), null, CancellationToken.None);
            }),
            Task.Run(async () =>
            {
                await using var db = CreateDbContext(tenantContext);
                var service = new RoleService(db, clock);
                await service.AssignRolesAsync(tenantId, userId, new List<Role> { Role.Gymnast }.AsReadOnly(), null, CancellationToken.None);
            })
        };

        await Task.WhenAll(tasks);

        // Assert - All roles should be assigned (no duplicates due to DB constraints)
        await using var verifyDb = CreateDbContext(tenantContext);
        var userRoles = await verifyDb.UserRoles
            .Where(ur => ur.KeycloakUserId == userId)
            .ToListAsync();

        userRoles.Should().HaveCount(3);
        userRoles.Select(ur => ur.Role).Should().Contain(new[] { Role.Coach, Role.ClubAdmin, Role.Gymnast });
    }

    [Fact]
    public async Task AssignRolesAsync_VeryLongUserId_HandlesCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = new string('a', 200); // Very long user ID
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
        userRoles[0].KeycloakUserId.Should().Be(userId);
    }

    [Fact]
    public async Task RemoveRoleAsync_RemoveLastRole_UserHasNoRoles()
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
        await service.RemoveRoleAsync(tenantId, userId, Role.Coach, CancellationToken.None);

        // Assert
        var hasAnyRole = await service.HasAnyRoleAsync(
            tenantId,
            userId,
            new List<Role> { Role.Gymnast, Role.Coach, Role.ClubAdmin, Role.IndividualAdmin }.AsReadOnly(),
            CancellationToken.None);
        hasAnyRole.Should().BeFalse();
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
