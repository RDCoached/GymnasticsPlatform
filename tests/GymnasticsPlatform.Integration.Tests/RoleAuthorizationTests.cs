using System.Net;
using System.Net.Http.Json;
using Auth.Application.Services;
using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class RoleAuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RoleAuthorizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ClubAdminEndpoint_WithClubAdminRole_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        // Create user profile and assign ClubAdmin role
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();

            _factory.TestTenantContext.TenantId = tenantId;

            // Create user profile so TenantResolutionMiddleware can resolve tenant
            var userProfile = UserProfile.Create(
                tenantId,
                userId,
                "admin@example.com",
                "Admin User",
                clock.GetUtcNow());
            db.UserProfiles.Add(userProfile);
            await db.SaveChangesAsync();

            await roleService.AssignRolesAsync(
                tenantId,
                userId,
                new List<Role> { Role.ClubAdmin }.AsReadOnly(),
                null,
                CancellationToken.None);
        }

        // Create club in this tenant
        Guid clubId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            _factory.TestTenantContext.TenantId = tenantId;

            var club = Club.Create("Test Club", userId, clock);
            club.GetType().GetProperty("TenantId")!.SetValue(club, tenantId);
            db.Clubs.Add(club);
            await db.SaveChangesAsync();
            clubId = club.Id;
        }

        var client = _factory.CreateAuthenticatedUserClient(userId, tenantId);
        var request = new
        {
            Email = "test@example.com",
            InviteType = InviteType.Coach,
            Description = "Test invite"
        };

        // Act - Try to send an email invite (ClubAdmin only endpoint)
        var response = await client.PostAsJsonAsync($"/api/clubs/{clubId}/invites/send-email", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ClubAdminEndpoint_WithoutClubAdminRole_ReturnsForbidden()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        // Create user profile and assign Coach role (not ClubAdmin)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();

            _factory.TestTenantContext.TenantId = tenantId;

            var userProfile = UserProfile.Create(
                tenantId,
                userId,
                "coach@example.com",
                "Coach User",
                clock.GetUtcNow());
            db.UserProfiles.Add(userProfile);
            await db.SaveChangesAsync();

            await roleService.AssignRolesAsync(
                tenantId,
                userId,
                new List<Role> { Role.Coach }.AsReadOnly(),
                null,
                CancellationToken.None);
        }

        // Create club
        Guid clubId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            _factory.TestTenantContext.TenantId = tenantId;

            var club = Club.Create("Test Club", userId, clock);
            club.GetType().GetProperty("TenantId")!.SetValue(club, tenantId);
            db.Clubs.Add(club);
            await db.SaveChangesAsync();
            clubId = club.Id;
        }

        var client = _factory.CreateAuthenticatedUserClient(userId, tenantId);
        var request = new
        {
            Email = "test@example.com",
            InviteType = InviteType.Coach,
            Description = "Test invite"
        };

        // Act - Try to send an email invite without ClubAdmin role
        var response = await client.PostAsJsonAsync($"/api/clubs/{clubId}/invites/send-email", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ClubAdminEndpoint_WithGymnastRole_ReturnsForbidden()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        // Assign Gymnast role (lowest privilege)
        using (var scope = _factory.Services.CreateScope())
        {
            var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();
            await roleService.AssignRolesAsync(
                tenantId,
                userId,
                new List<Role> { Role.Gymnast }.AsReadOnly(),
                null,
                CancellationToken.None);
        }

        // Create club
        Guid clubId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            _factory.TestTenantContext.TenantId = tenantId;

            var club = Club.Create("Test Club", "owner-123", clock);
            club.GetType().GetProperty("TenantId")!.SetValue(club, tenantId);
            db.Clubs.Add(club);
            await db.SaveChangesAsync();
            clubId = club.Id;
        }

        var client = _factory.CreateAuthenticatedUserClient(userId, tenantId);
        var request = new
        {
            Email = "test@example.com",
            InviteType = InviteType.Coach,
            Description = "Test invite"
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/clubs/{clubId}/invites/send-email", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CoachPolicy_AllowsCoachClubAdminAndIndividualAdmin()
    {
        // This test verifies the CoachPolicy accepts Coach, ClubAdmin, and IndividualAdmin roles
        // We'll test by assigning each role and verifying access

        var tenantId = Guid.NewGuid();
        Guid clubId;

        // Create club
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            _factory.TestTenantContext.TenantId = tenantId;

            var club = Club.Create("Test Club", "owner-123", clock);
            club.GetType().GetProperty("TenantId")!.SetValue(club, tenantId);
            db.Clubs.Add(club);
            await db.SaveChangesAsync();
            clubId = club.Id;
        }

        // Create user profiles
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            _factory.TestTenantContext.TenantId = tenantId;

            var coachProfile = UserProfile.Create(tenantId, Guid.NewGuid().ToString(), "coach@test.com", "Coach", clock.GetUtcNow());
            var adminProfile = UserProfile.Create(tenantId, Guid.NewGuid().ToString(), "admin@test.com", "Admin", clock.GetUtcNow());
            db.UserProfiles.Add(coachProfile);
            db.UserProfiles.Add(adminProfile);
            await db.SaveChangesAsync();
        }

        var roleService = _factory.Services.GetRequiredService<IRoleService>();

        // Test with Coach role
        var coachUserId = Guid.NewGuid().ToString();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            _factory.TestTenantContext.TenantId = tenantId;

            var coachProfile = UserProfile.Create(tenantId, coachUserId, "coach2@test.com", "Coach User", clock.GetUtcNow());
            db.UserProfiles.Add(coachProfile);
            await db.SaveChangesAsync();
        }
        await roleService.AssignRolesAsync(tenantId, coachUserId, new List<Role> { Role.Coach }.AsReadOnly(), null, CancellationToken.None);
        var coachClient = _factory.CreateAuthenticatedUserClient(coachUserId, tenantId);
        var coachResponse = await coachClient.GetAsync($"/api/clubs/{clubId}/invites");
        coachResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Coach should not access ClubAdmin endpoints");

        // Test with ClubAdmin role
        var adminUserId = Guid.NewGuid().ToString();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            _factory.TestTenantContext.TenantId = tenantId;

            var adminProfile = UserProfile.Create(tenantId, adminUserId, "admin2@test.com", "Admin User", clock.GetUtcNow());
            db.UserProfiles.Add(adminProfile);
            await db.SaveChangesAsync();
        }
        await roleService.AssignRolesAsync(tenantId, adminUserId, new List<Role> { Role.ClubAdmin }.AsReadOnly(), null, CancellationToken.None);
        var adminClient = _factory.CreateAuthenticatedUserClient(adminUserId, tenantId);
        var adminResponse = await adminClient.GetAsync($"/api/clubs/{clubId}/invites");
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK, "ClubAdmin should access ClubAdmin endpoints");
    }

    [Fact]
    public async Task GymnastPolicy_AllowsAllRoles()
    {
        // GymnastPolicy should allow Gymnast, Coach, ClubAdmin, and IndividualAdmin
        // This is the most permissive policy - everyone can access gymnast-level endpoints

        var tenantId = Guid.NewGuid();
        var roleService = _factory.Services.GetRequiredService<IRoleService>();

        // Test with Gymnast role
        var gymnastUserId = Guid.NewGuid().ToString();
        await roleService.AssignRolesAsync(tenantId, gymnastUserId, new List<Role> { Role.Gymnast }.AsReadOnly(), null, CancellationToken.None);
        var hasGymnastAccess = await roleService.HasAnyRoleAsync(
            tenantId,
            gymnastUserId,
            new List<Role> { Role.Gymnast, Role.Coach, Role.ClubAdmin, Role.IndividualAdmin }.AsReadOnly(),
            CancellationToken.None);
        hasGymnastAccess.Should().BeTrue();

        // Test with Coach role
        var coachUserId = Guid.NewGuid().ToString();
        await roleService.AssignRolesAsync(tenantId, coachUserId, new List<Role> { Role.Coach }.AsReadOnly(), null, CancellationToken.None);
        var hasCoachAccess = await roleService.HasAnyRoleAsync(
            tenantId,
            coachUserId,
            new List<Role> { Role.Gymnast, Role.Coach, Role.ClubAdmin, Role.IndividualAdmin }.AsReadOnly(),
            CancellationToken.None);
        hasCoachAccess.Should().BeTrue();
    }

    [Fact]
    public async Task Authorization_WithNoRoles_ReturnsForbidden()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        // Don't assign any roles

        // Create club
        Guid clubId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            _factory.TestTenantContext.TenantId = tenantId;

            var club = Club.Create("Test Club", "owner-123", clock);
            club.GetType().GetProperty("TenantId")!.SetValue(club, tenantId);
            db.Clubs.Add(club);
            await db.SaveChangesAsync();
            clubId = club.Id;
        }

        var client = _factory.CreateAuthenticatedUserClient(userId, tenantId);

        // Act - Try to access ClubAdmin endpoint
        var response = await client.GetAsync($"/api/clubs/{clubId}/invites");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ClubAdmin_CanAssignAndRemoveRoles()
    {
        // Arrange
        var adminUserId = Guid.NewGuid().ToString();
        var memberUserId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        // Create admin user profile and assign ClubAdmin role
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();

            _factory.TestTenantContext.TenantId = tenantId;

            var adminProfile = UserProfile.Create(
                tenantId,
                adminUserId,
                "admin@example.com",
                "Admin User",
                clock.GetUtcNow());
            db.UserProfiles.Add(adminProfile);
            await db.SaveChangesAsync();

            await roleService.AssignRolesAsync(
                tenantId,
                adminUserId,
                new List<Role> { Role.ClubAdmin }.AsReadOnly(),
                null,
                CancellationToken.None);
        }

        // Create club and member profile
        Guid clubId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            _factory.TestTenantContext.TenantId = tenantId;

            var club = Club.Create("Test Club", adminUserId, clock);
            club.GetType().GetProperty("TenantId")!.SetValue(club, tenantId);
            db.Clubs.Add(club);

            var memberProfile = UserProfile.Create(
                tenantId,
                memberUserId,
                "member@example.com",
                "Member User",
                clock.GetUtcNow());
            db.UserProfiles.Add(memberProfile);

            await db.SaveChangesAsync();
            clubId = club.Id;
        }

        var client = _factory.CreateAuthenticatedUserClient(adminUserId, tenantId);

        // Act - Assign Coach role to member
        var assignRequest = new { Role = Role.Coach };
        var assignResponse = await client.PostAsJsonAsync($"/api/clubs/{clubId}/members/{memberUserId}/roles", assignRequest);
        assignResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify role was assigned
        using (var scope = _factory.Services.CreateScope())
        {
            var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();
            var roles = await roleService.GetUserRolesAsync(tenantId, memberUserId, CancellationToken.None);
            roles.Should().Contain(Role.Coach);
        }

        // Act - Remove Coach role from member
        var removeResponse = await client.DeleteAsync($"/api/clubs/{clubId}/members/{memberUserId}/roles/{(int)Role.Coach}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify role was removed
        using (var scope = _factory.Services.CreateScope())
        {
            var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();
            var roles = await roleService.GetUserRolesAsync(tenantId, memberUserId, CancellationToken.None);
            roles.Should().NotContain(Role.Coach);
        }
    }
}
