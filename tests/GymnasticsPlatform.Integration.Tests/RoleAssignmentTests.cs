using System.Net;
using System.Net.Http.Json;
using Auth.Application.Services;
using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using Common.Core.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class RoleAssignmentTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RoleAssignmentTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateClub_AssignsClubAdminAndCoachRoles()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var client = _factory.CreateOnboardingUserClient(userId);
        var request = new { Name = "Test Club" };

        // CreateClient must be called before accessing TestTenantContext
        _factory.TestTenantContext.TenantId = TenantConstants.OnboardingTenantId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var userProfile = UserProfile.Create(
                TenantConstants.OnboardingTenantId,
                userId,
                "test@example.com",
                "Test User",
                clock.GetUtcNow());
            db.UserProfiles.Add(userProfile);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.PostAsJsonAsync("/api/onboarding/create-club", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<OnboardingResponse>();
        result.Should().NotBeNull();

        // Verify roles were assigned
        using var verifyScope = _factory.Services.CreateScope();
        var roleService = verifyScope.ServiceProvider.GetRequiredService<IRoleService>();
        var roles = await roleService.GetUserRolesAsync(result!.TenantId, userId, CancellationToken.None);

        roles.Should().HaveCount(2);
        roles.Should().Contain(Role.ClubAdmin);
        roles.Should().Contain(Role.Coach);
    }

    [Fact]
    public async Task JoinClub_WithCoachInvite_AssignsCoachRole()
    {
        // Arrange
        var ownerId = Guid.NewGuid().ToString();
        var joinerId = Guid.NewGuid().ToString();
        string inviteCode;
        Guid clubTenantId;

        // Create club with coach invite
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var club = Club.Create("Test Club", ownerId, clock);
            _factory.TestTenantContext.TenantId = club.TenantId;
            db.Clubs.Add(club);

            var invite = ClubInvite.Create(
                club.Id,
                InviteType.Coach,
                maxUses: 5,
                expiresAt: clock.GetUtcNow().AddDays(7),
                null,
                null,
                clock);
            db.ClubInvites.Add(invite);
            await db.SaveChangesAsync();

            inviteCode = invite.Code;
            clubTenantId = club.TenantId;
        }

        // Create joiner profile
        _factory.TestTenantContext.TenantId = TenantConstants.OnboardingTenantId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var joinerProfile = UserProfile.Create(
                TenantConstants.OnboardingTenantId,
                joinerId,
                "joiner@example.com",
                "Joiner User",
                clock.GetUtcNow());
            db.UserProfiles.Add(joinerProfile);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateOnboardingUserClient(joinerId);
        var request = new { InviteCode = inviteCode };

        // Act
        var response = await client.PostAsJsonAsync("/api/onboarding/join-club", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify Coach role was assigned
        using var verifyScope = _factory.Services.CreateScope();
        var roleService = verifyScope.ServiceProvider.GetRequiredService<IRoleService>();
        var roles = await roleService.GetUserRolesAsync(clubTenantId, joinerId, CancellationToken.None);

        roles.Should().HaveCount(1);
        roles.Should().Contain(Role.Coach);
    }

    [Fact]
    public async Task JoinClub_WithGymnastInvite_AssignsGymnastRole()
    {
        // Arrange
        var ownerId = Guid.NewGuid().ToString();
        var joinerId = Guid.NewGuid().ToString();
        string inviteCode;
        Guid clubTenantId;

        // Create club with gymnast invite
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var club = Club.Create("Test Club", ownerId, clock);
            _factory.TestTenantContext.TenantId = club.TenantId;
            db.Clubs.Add(club);

            var invite = ClubInvite.Create(
                club.Id,
                InviteType.Gymnast,
                maxUses: 5,
                expiresAt: clock.GetUtcNow().AddDays(7),
                null,
                null,
                clock);
            db.ClubInvites.Add(invite);
            await db.SaveChangesAsync();

            inviteCode = invite.Code;
            clubTenantId = club.TenantId;
        }

        // Create joiner profile
        _factory.TestTenantContext.TenantId = TenantConstants.OnboardingTenantId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var joinerProfile = UserProfile.Create(
                TenantConstants.OnboardingTenantId,
                joinerId,
                "gymnast@example.com",
                "Gymnast User",
                clock.GetUtcNow());
            db.UserProfiles.Add(joinerProfile);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateOnboardingUserClient(joinerId);
        var request = new { InviteCode = inviteCode };

        // Act
        var response = await client.PostAsJsonAsync("/api/onboarding/join-club", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify Gymnast role was assigned
        using var verifyScope = _factory.Services.CreateScope();
        var roleService = verifyScope.ServiceProvider.GetRequiredService<IRoleService>();
        var roles = await roleService.GetUserRolesAsync(clubTenantId, joinerId, CancellationToken.None);

        roles.Should().HaveCount(1);
        roles.Should().Contain(Role.Gymnast);
    }

    [Fact]
    public async Task IndividualMode_AssignsIndividualAdminAndCoachRoles()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var client = _factory.CreateOnboardingUserClient(userId);

        _factory.TestTenantContext.TenantId = TenantConstants.OnboardingTenantId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var userProfile = UserProfile.Create(
                TenantConstants.OnboardingTenantId,
                userId,
                "individual@example.com",
                "Individual User",
                clock.GetUtcNow());
            db.UserProfiles.Add(userProfile);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.PostAsync("/api/onboarding/individual", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<OnboardingResponse>();
        result.Should().NotBeNull();

        // Verify roles were assigned
        using var verifyScope = _factory.Services.CreateScope();
        var roleService = verifyScope.ServiceProvider.GetRequiredService<IRoleService>();
        var roles = await roleService.GetUserRolesAsync(result!.TenantId, userId, CancellationToken.None);

        roles.Should().HaveCount(2);
        roles.Should().Contain(Role.IndividualAdmin);
        roles.Should().Contain(Role.Coach);
    }

    [Fact]
    public async Task RolesAreTenantScoped_UserHasDifferentRolesInDifferentTenants()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();

        // Assign ClubAdmin role in tenant 1
        await roleService.AssignRolesAsync(
            tenant1Id,
            userId,
            new List<Role> { Role.ClubAdmin }.AsReadOnly(),
            null,
            CancellationToken.None);

        // Assign Gymnast role in tenant 2
        await roleService.AssignRolesAsync(
            tenant2Id,
            userId,
            new List<Role> { Role.Gymnast }.AsReadOnly(),
            null,
            CancellationToken.None);

        // Act & Assert - Verify tenant 1 roles
        var tenant1Roles = await roleService.GetUserRolesAsync(tenant1Id, userId, CancellationToken.None);
        tenant1Roles.Should().HaveCount(1);
        tenant1Roles.Should().Contain(Role.ClubAdmin);
        tenant1Roles.Should().NotContain(Role.Gymnast);

        // Act & Assert - Verify tenant 2 roles
        var tenant2Roles = await roleService.GetUserRolesAsync(tenant2Id, userId, CancellationToken.None);
        tenant2Roles.Should().HaveCount(1);
        tenant2Roles.Should().Contain(Role.Gymnast);
        tenant2Roles.Should().NotContain(Role.ClubAdmin);
    }

    private record OnboardingResponse(Guid TenantId, IReadOnlyList<Role> Roles, Guid? ClubId);
}
