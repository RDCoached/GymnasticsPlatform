using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using Auth.Infrastructure.Services;
using Common.Core;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Auth.Infrastructure.Tests.Services;

[Collection("Database collection")]
public sealed class UserTenantServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;

    public UserTenantServiceTests(DatabaseFixture fixture)
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
    public async Task GetUserTenantIdAsync_ExistingUser_ReturnsTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);

        var userProfile = UserProfile.Create(
            tenantId,
            userId,
            "test@example.com",
            "Test User",
            clock.GetUtcNow());
        db.UserProfiles.Add(userProfile);
        await db.SaveChangesAsync();

        var logger = Substitute.For<ILogger<UserTenantService>>();
        var service = new UserTenantService(db, clock, logger);

        // Act
        var result = await service.GetUserTenantIdAsync(userId);

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public async Task GetUserTenantIdAsync_NonExistentUser_ReturnsNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "non-existent-user";
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);

        var logger = Substitute.For<ILogger<UserTenantService>>();
        var service = new UserTenantService(db, clock, logger);

        // Act
        var result = await service.GetUserTenantIdAsync(userId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserTenantIdAsync_EmptyUserId_ReturnsNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);

        var logger = Substitute.For<ILogger<UserTenantService>>();
        var service = new UserTenantService(db, clock, logger);

        // Act
        var result = await service.GetUserTenantIdAsync(string.Empty);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateUserTenantAsync_ExistingUser_UpdatesTenant()
    {
        // Arrange
        var oldTenantId = Guid.NewGuid();
        var newTenantId = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(oldTenantId);
        await using var db = CreateDbContext(tenantContext);

        var userProfile = UserProfile.Create(
            oldTenantId,
            userId,
            "test@example.com",
            "Test User",
            clock.GetUtcNow());
        db.UserProfiles.Add(userProfile);
        await db.SaveChangesAsync();

        var logger = Substitute.For<ILogger<UserTenantService>>();
        var service = new UserTenantService(db, clock, logger);

        // Act
        await service.UpdateUserTenantAsync(userId, newTenantId);

        // Assert
        var updated = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstAsync(u => u.ProviderUserId == userId);
        updated.TenantId.Should().Be(newTenantId);
        // Note: AuthProvider call now happens via domain event handler
    }

    [Fact]
    public async Task UpdateUserTenantAsync_NonExistentUserWithEmailAndName_CreatesProfile()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "new-user-123";
        var email = "newuser@example.com";
        var fullName = "New User";
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);

        var logger = Substitute.For<ILogger<UserTenantService>>();
        var service = new UserTenantService(db, clock, logger);

        // Act
        await service.UpdateUserTenantAsync(userId, tenantId, email, fullName);

        // Assert
        var created = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.ProviderUserId == userId);

        created.Should().NotBeNull();
        created!.TenantId.Should().Be(tenantId);
        created.Email.Should().Be(email);
        created.FullName.Should().Be(fullName);
        // Note: AuthProvider call now happens via domain event handler
    }

    [Fact]
    public async Task UpdateUserTenantAsync_NonExistentUserWithoutEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "new-user-123";
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);

        var logger = Substitute.For<ILogger<UserTenantService>>();
        var service = new UserTenantService(db, clock, logger);

        // Act
        var act = async () => await service.UpdateUserTenantAsync(userId, tenantId, null, "Name");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Email*required*");
    }

    [Fact]
    public async Task UpdateUserTenantAsync_NonExistentUserWithoutFullName_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "new-user-123";
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);

        var logger = Substitute.For<ILogger<UserTenantService>>();
        var service = new UserTenantService(db, clock, logger);

        // Act
        var act = async () => await service.UpdateUserTenantAsync(userId, tenantId, "email@test.com", null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*full name*required*");
    }

    [Fact]
    public async Task UpdateUserTenantAsync_NonExistentUserWithEmptyEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "new-user-123";
        var clock = TimeProvider.System;

        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);

        var logger = Substitute.For<ILogger<UserTenantService>>();
        var service = new UserTenantService(db, clock, logger);

        // Act
        var act = async () => await service.UpdateUserTenantAsync(userId, tenantId, string.Empty, "Name");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Email*required*");
    }

    // Test removed: UpdateUserTenantAsync_UpdatesKeycloakService
    // Reason: AuthProvider is no longer called directly - it's now handled by UserTenantUpdatedHandler
    // This behavior is tested in integration tests with the full event pipeline

    [Fact]
    public async Task GetUserTenantIdAsync_IgnoresQueryFilters_CanFindUserInAnyTenant()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();
        var userId = "keycloak-user-123";
        var clock = TimeProvider.System;

        // Create user in tenant1
        var tenant1Context = CreateTenantContext(tenant1Id);
        await using (var db = CreateDbContext(tenant1Context))
        {
            var userProfile = UserProfile.Create(
                tenant1Id,
                userId,
                "test@example.com",
                "Test User",
                clock.GetUtcNow());
            db.UserProfiles.Add(userProfile);
            await db.SaveChangesAsync();
        }

        // Query from tenant2 context - should still find user due to IgnoreQueryFilters
        var tenant2Context = CreateTenantContext(tenant2Id);
        await using (var db = CreateDbContext(tenant2Context))
        {
            var logger = Substitute.For<ILogger<UserTenantService>>();
            var service = new UserTenantService(db, clock, logger);

            // Act
            var result = await service.GetUserTenantIdAsync(userId);

            // Assert
            result.Should().Be(tenant1Id);
        }
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
