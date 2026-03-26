using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using Common.Core;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace Auth.Infrastructure.Tests;

[CollectionDefinition("Database collection")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Create database schema once for all tests
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(Guid.NewGuid());

        await using var db = new AuthDbContext(options, tenantContext);
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[Collection("Database collection")]
public sealed class AuthDbContextTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;

    public AuthDbContextTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private string ConnectionString => _fixture.ConnectionString;

    public Task InitializeAsync()
    {
        // Schema is created once in DatabaseFixture
        // No per-test initialization needed
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Clean up database by truncating all tables
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "TRUNCATE TABLE \"UserProfiles\" RESTART IDENTITY CASCADE";
        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Table doesn't exist - ignore (happens on first test)
        }
    }

    [Fact]
    public async Task SaveChangesAsync_AutoSetsTenantId_OnNewEntities()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateDbContext(tenantContext);
        await db.Database.MigrateAsync();

        var userProfile = UserProfile.Create(
            Guid.Empty, // Will be overwritten
            "keycloak-123",
            "test@example.com",
            "Test User",
            DateTimeOffset.UtcNow);

        // Act
        db.UserProfiles.Add(userProfile);
        await db.SaveChangesAsync();

        // Assert
        userProfile.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task SaveChangesAsync_ThrowsException_WhenTenantIdIsNull()
    {
        // Arrange
        var tenantContext = CreateTenantContext(null);
        await using var db = CreateDbContext(tenantContext);
        await db.Database.MigrateAsync();

        var userProfile = UserProfile.Create(
            Guid.Empty,
            "keycloak-123",
            "test@example.com",
            "Test User",
            DateTimeOffset.UtcNow);

        db.UserProfiles.Add(userProfile);

        // Act & Assert
        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("TenantId is required for creating multi-tenant entities");
    }

    [Fact]
    public async Task QueryFilter_OnlyReturnsEntitiesForCurrentTenant()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        // Create users for tenant 1
        var tenant1Context = CreateTenantContext(tenant1Id);
        await using (var db = CreateDbContext(tenant1Context))
        {
            await db.Database.MigrateAsync();

            var user1 = UserProfile.Create(tenant1Id, "user1", "user1@tenant1.com", "User 1", DateTimeOffset.UtcNow);
            var user2 = UserProfile.Create(tenant1Id, "user2", "user2@tenant1.com", "User 2", DateTimeOffset.UtcNow);

            db.UserProfiles.AddRange(user1, user2);
            await db.SaveChangesAsync();
        }

        // Create users for tenant 2
        var tenant2Context = CreateTenantContext(tenant2Id);
        await using (var db = CreateDbContext(tenant2Context))
        {
            var user3 = UserProfile.Create(tenant2Id, "user3", "user3@tenant2.com", "User 3", DateTimeOffset.UtcNow);

            db.UserProfiles.Add(user3);
            await db.SaveChangesAsync();
        }

        // Act - Query as tenant 1
        await using (var db = CreateDbContext(tenant1Context))
        {
            var users = await db.UserProfiles.ToListAsync();

            // Assert - Should only see tenant 1 users
            users.Should().HaveCount(2);
            users.Should().AllSatisfy(u => u.TenantId.Should().Be(tenant1Id));
            users.Select(u => u.Email).Should().Contain("user1@tenant1.com");
            users.Select(u => u.Email).Should().Contain("user2@tenant1.com");
        }

        // Act - Query as tenant 2
        await using (var db = CreateDbContext(tenant2Context))
        {
            var users = await db.UserProfiles.ToListAsync();

            // Assert - Should only see tenant 2 users
            users.Should().HaveCount(1);
            users[0].TenantId.Should().Be(tenant2Id);
            users[0].Email.Should().Be("user3@tenant2.com");
        }
    }

    [Fact]
    public async Task QueryFilter_FindAsync_OnlyReturnsEntitiesForCurrentTenant()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        Guid userId;

        // Create user for tenant 1
        var tenant1Context = CreateTenantContext(tenant1Id);
        await using (var db = CreateDbContext(tenant1Context))
        {
            await db.Database.MigrateAsync();

            var user = UserProfile.Create(tenant1Id, "user1", "user1@tenant1.com", "User 1", DateTimeOffset.UtcNow);
            db.UserProfiles.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        // Act - Try to find tenant 1's user while in tenant 2 context
        var tenant2Context = CreateTenantContext(tenant2Id);
        await using (var db = CreateDbContext(tenant2Context))
        {
            var user = await db.UserProfiles.FindAsync(userId);

            // Assert - Should NOT find the user (different tenant)
            user.Should().BeNull();
        }

        // Act - Find user in correct tenant context
        await using (var db = CreateDbContext(tenant1Context))
        {
            var user = await db.UserProfiles.FindAsync(userId);

            // Assert - Should find the user
            user.Should().NotBeNull();
            user!.TenantId.Should().Be(tenant1Id);
        }
    }

    [Fact]
    public async Task QueryFilter_FirstOrDefaultAsync_OnlyReturnsEntitiesForCurrentTenant()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        // Create users for both tenants with same email pattern
        var tenant1Context = CreateTenantContext(tenant1Id);
        await using (var db = CreateDbContext(tenant1Context))
        {
            await db.Database.MigrateAsync();
            var user = UserProfile.Create(tenant1Id, "user1", "test@example.com", "Tenant 1 User", DateTimeOffset.UtcNow);
            db.UserProfiles.Add(user);
            await db.SaveChangesAsync();
        }

        var tenant2Context = CreateTenantContext(tenant2Id);
        await using (var db = CreateDbContext(tenant2Context))
        {
            var user = UserProfile.Create(tenant2Id, "user2", "test@example.com", "Tenant 2 User", DateTimeOffset.UtcNow);
            db.UserProfiles.Add(user);
            await db.SaveChangesAsync();
        }

        // Act - Query by email in tenant 1 context
        await using (var db = CreateDbContext(tenant1Context))
        {
            var user = await db.UserProfiles
                .FirstOrDefaultAsync(u => u.Email == "test@example.com");

            // Assert - Should find tenant 1's user
            user.Should().NotBeNull();
            user!.FullName.Should().Be("Tenant 1 User");
            user.TenantId.Should().Be(tenant1Id);
        }

        // Act - Query by email in tenant 2 context
        await using (var db = CreateDbContext(tenant2Context))
        {
            var user = await db.UserProfiles
                .FirstOrDefaultAsync(u => u.Email == "test@example.com");

            // Assert - Should find tenant 2's user
            user.Should().NotBeNull();
            user!.FullName.Should().Be("Tenant 2 User");
            user.TenantId.Should().Be(tenant2Id);
        }
    }

    [Fact]
    public async Task QueryFilter_CountAsync_OnlyCountsEntitiesForCurrentTenant()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        // Create 3 users for tenant 1
        var tenant1Context = CreateTenantContext(tenant1Id);
        await using (var db = CreateDbContext(tenant1Context))
        {
            await db.Database.MigrateAsync();
            for (int i = 0; i < 3; i++)
            {
                var user = UserProfile.Create(tenant1Id, $"user{i}", $"user{i}@tenant1.com", $"User {i}", DateTimeOffset.UtcNow);
                db.UserProfiles.Add(user);
            }
            await db.SaveChangesAsync();
        }

        // Create 5 users for tenant 2
        var tenant2Context = CreateTenantContext(tenant2Id);
        await using (var db = CreateDbContext(tenant2Context))
        {
            for (int i = 0; i < 5; i++)
            {
                var user = UserProfile.Create(tenant2Id, $"user{i}", $"user{i}@tenant2.com", $"User {i}", DateTimeOffset.UtcNow);
                db.UserProfiles.Add(user);
            }
            await db.SaveChangesAsync();
        }

        // Act & Assert - Count for tenant 1
        await using (var db = CreateDbContext(tenant1Context))
        {
            var count = await db.UserProfiles.CountAsync();
            count.Should().Be(3);
        }

        // Act & Assert - Count for tenant 2
        await using (var db = CreateDbContext(tenant2Context))
        {
            var count = await db.UserProfiles.CountAsync();
            count.Should().Be(5);
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
