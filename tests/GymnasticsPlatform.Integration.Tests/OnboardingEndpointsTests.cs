using System.Net;
using System.Net.Http.Json;
using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using Common.Core.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class OnboardingEndpointsTests : IClassFixture<TestWebApplicationFactory>
{

    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OnboardingEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record OnboardingCompleteResponse(Guid TenantId, IReadOnlyList<Role> Roles, Guid? ClubId);

    [Fact]
    public async Task GetOnboardingStatus_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/onboarding/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FirstTimeUser_AutomaticallyGetsProfileInOnboardingTenant()
    {
        // Arrange - Brand new user with NO profile in database (simulating first login)
        var userId = Guid.NewGuid().ToString();
        var client = _factory.CreateOnboardingUserClient(userId);
        // DON'T create profile - this simulates a first-time Google OAuth user

        // Act - Make first authenticated request (middleware should auto-create profile)
        var response = await client.GetAsync("/api/onboarding/status");

        // Assert - Request should succeed
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Request failed with {response.StatusCode}. Error: {errorContent}");
        }
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify profile was auto-created in onboarding tenant
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        // Check all profiles in database for debugging
        var allProfiles = await db.UserProfiles.IgnoreQueryFilters().ToListAsync();
        Console.WriteLine($"Total profiles in DB: {allProfiles.Count}");
        foreach (var p in allProfiles)
        {
            Console.WriteLine($"  Profile: UserId={p.KeycloakUserId}, TenantId={p.TenantId}");
        }

        var profile = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.KeycloakUserId == userId);

        profile.Should().NotBeNull($"Profile should exist for user {userId}");
        profile!.TenantId.Should().Be(TenantConstants.OnboardingTenantId);
        profile.Email.Should().Be("test@example.com"); // From test auth headers
        profile.OnboardingCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateClub_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var request = new { Name = "Test Club" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/onboarding/create-club", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JoinClub_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var request = new { InviteCode = "TEST1234" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/onboarding/join-club", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChooseIndividualMode_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsync("/api/onboarding/individual", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullFlow_CreateClub_CompletesOnboardingAndReturnsNewTenantId()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var client = _factory.CreateOnboardingUserClient(userId);
        var request = new { Name = "Elite Gymnastics Academy" };

        // Set tenant context to onboarding tenant for creating test data
        _factory.TestTenantContext.TenantId = TenantConstants.OnboardingTenantId;

        // Create user profile in onboarding state
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var userProfile = Auth.Domain.Entities.UserProfile.Create(
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

        var result = await response.Content.ReadFromJsonAsync<OnboardingCompleteResponse>();
        result.Should().NotBeNull();
        result!.TenantId.Should().NotBeEmpty();
        result.TenantId.Should().NotBe(TenantConstants.OnboardingTenantId);
        result.Roles.Should().Contain(Role.ClubAdmin);
        result.Roles.Should().Contain(Role.Coach);
        result.ClubId.Should().NotBeNull();
        result.ClubId!.Value.Should().NotBeEmpty();

        // Verify in a fresh scope to see committed changes
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

            // Verify club was created in database (ignore query filters to see all tenants)
            var club = await db.Clubs.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == result.ClubId.Value);
            club.Should().NotBeNull();
            club!.Name.Should().Be("Elite Gymnastics Academy");
            club.TenantId.Should().Be(result.TenantId);
            club.OwnerUserId.Should().Be(userId);

            // Verify user onboarding status was updated
            var updatedProfile = await db.UserProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.KeycloakUserId == userId);
            updatedProfile.Should().NotBeNull();
            updatedProfile!.OnboardingCompleted.Should().BeTrue();
            updatedProfile.OnboardingChoice.Should().Be("club");
        }
    }

    [Fact]
    public async Task FullFlow_JoinClub_AssignsUserToClubTenantAndMarksInviteUsed()
    {
        // Arrange
        var ownerId = Guid.NewGuid().ToString();
        var joinerId = Guid.NewGuid().ToString();
        string inviteCode;
        Guid clubId;
        Guid clubTenantId;
        Guid inviteId;

        // Create club and invite in setup scope
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            // Create club with its own tenant ID
            var club = Auth.Domain.Entities.Club.Create("Test Gymnastics Club", ownerId, clock);
            _factory.TestTenantContext.TenantId = club.TenantId;
            db.Clubs.Add(club);

            var invite = Auth.Domain.Entities.ClubInvite.Create(
                club.Id,
                InviteType.Coach,
                maxUses: 5,
                expiresAt: clock.GetUtcNow().AddDays(7),
                null,
                null,
                clock);
            db.ClubInvites.Add(invite);
            await db.SaveChangesAsync();

            // Store values for later use
            inviteCode = invite.Code;
            clubId = club.Id;
            clubTenantId = club.TenantId;
            inviteId = invite.Id;
        }

        // Create joiner profile in onboarding state
        _factory.TestTenantContext.TenantId = TenantConstants.OnboardingTenantId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var joinerProfile = Auth.Domain.Entities.UserProfile.Create(
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

        var result = await response.Content.ReadFromJsonAsync<OnboardingCompleteResponse>();
        result.Should().NotBeNull();
        result!.TenantId.Should().Be(clubTenantId);
        result.Roles.Should().Contain(Role.Coach);
        result.ClubId.Should().Be(clubId);

        // Verify in a fresh scope to see committed changes
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

            // Verify invite was marked as used (ignore query filters to see all tenants)
            var updatedInvite = await db.ClubInvites.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == inviteId);
            updatedInvite.Should().NotBeNull();
            updatedInvite!.TimesUsed.Should().Be(1);

            // Verify user onboarding status was updated
            var updatedProfile = await db.UserProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.KeycloakUserId == joinerId);
            updatedProfile.Should().NotBeNull();
            updatedProfile!.OnboardingCompleted.Should().BeTrue();
            updatedProfile.OnboardingChoice.Should().Be("club");
        }
    }

    [Fact]
    public async Task FullFlow_IndividualMode_CreatesUniqueTenantForUser()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var client = _factory.CreateOnboardingUserClient(userId);

        // Set tenant context to onboarding tenant for creating test data
        _factory.TestTenantContext.TenantId = TenantConstants.OnboardingTenantId;

        // Create user profile in onboarding state
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var userProfile = Auth.Domain.Entities.UserProfile.Create(
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

        var result = await response.Content.ReadFromJsonAsync<OnboardingCompleteResponse>();
        result.Should().NotBeNull();
        result!.TenantId.Should().NotBeEmpty();
        result.TenantId.Should().NotBe(TenantConstants.OnboardingTenantId);
        result.Roles.Should().Contain(Role.IndividualAdmin);
        result.Roles.Should().Contain(Role.Coach);
        result.ClubId.Should().BeNull();

        // Verify in a fresh scope to see committed changes
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

            // Verify user onboarding status was updated (ignore query filters to see all tenants)
            var updatedProfile = await db.UserProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.KeycloakUserId == userId);
            updatedProfile.Should().NotBeNull();
            updatedProfile!.OnboardingCompleted.Should().BeTrue();
            updatedProfile.OnboardingChoice.Should().Be("individual");

            // Verify tenant ID is unique (not used by any club)
            var clubWithSameTenant = await db.Clubs.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.TenantId == result.TenantId);
            clubWithSameTenant.Should().BeNull();
        }
    }

    [Fact]
    public async Task FullFlow_CreateClub_SubsequentRequestsUseScopedToNewTenant()
    {
        // Arrange - Create user in onboarding state
        var userId = Guid.NewGuid().ToString();
        var onboardingClient = _factory.CreateOnboardingUserClient(userId);

        _factory.TestTenantContext.TenantId = TenantConstants.OnboardingTenantId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var userProfile = Auth.Domain.Entities.UserProfile.Create(
                TenantConstants.OnboardingTenantId,
                userId,
                "test@example.com",
                "Test User",
                clock.GetUtcNow());
            db.UserProfiles.Add(userProfile);
            await db.SaveChangesAsync();
        }

        // Act - Complete onboarding by creating a club
        var createClubRequest = new { Name = "Test Gymnastics Club" };
        var createClubResponse = await onboardingClient.PostAsJsonAsync("/api/onboarding/create-club", createClubRequest);
        createClubResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var onboardingResult = await createClubResponse.Content.ReadFromJsonAsync<OnboardingCompleteResponse>();
        var newTenantId = onboardingResult!.TenantId;

        // Create a NEW client with the updated tenant (simulating re-authentication)
        var authenticatedClient = _factory.CreateAuthenticatedUserClient(userId, newTenantId);

        // Create a test entity in the new tenant to verify scoping
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            _factory.TestTenantContext.TenantId = newTenantId;

            var otherProfile = Auth.Domain.Entities.UserProfile.Create(
                newTenantId,
                Guid.NewGuid().ToString(),
                "other@example.com",
                "Other User",
                clock.GetUtcNow());
            db.UserProfiles.Add(otherProfile);
            await db.SaveChangesAsync();
        }

        // Create a user in a DIFFERENT tenant (should not be visible)
        var differentTenantId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            _factory.TestTenantContext.TenantId = differentTenantId;

            var differentTenantProfile = Auth.Domain.Entities.UserProfile.Create(
                differentTenantId,
                Guid.NewGuid().ToString(),
                "different@example.com",
                "Different Tenant User",
                clock.GetUtcNow());
            db.UserProfiles.Add(differentTenantProfile);
            await db.SaveChangesAsync();
        }

        // Assert - Verify subsequent requests are scoped to the new tenant
        var statusResponse = await authenticatedClient.GetAsync("/api/onboarding/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the user can only see data from their tenant
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            _factory.TestTenantContext.TenantId = newTenantId;

            // Should see 2 profiles in new tenant (original user + other user)
            var profilesInTenant = await db.UserProfiles.ToListAsync();
            profilesInTenant.Should().HaveCount(2);
            profilesInTenant.Should().AllSatisfy(p => p.TenantId.Should().Be(newTenantId));

            // Should NOT see the user from the different tenant
            profilesInTenant.Should().NotContain(p => p.Email == "different@example.com");

            // Should NOT see any users from onboarding tenant
            profilesInTenant.Should().NotContain(p => p.TenantId == TenantConstants.OnboardingTenantId);
        }

        // Verify user is NO LONGER in onboarding state
        // (Attempting to create another club should fail)
        var secondClubRequest = new { Name = "Second Club Should Fail" };
        var secondClubResponse = await authenticatedClient.PostAsJsonAsync("/api/onboarding/create-club", secondClubRequest);
        secondClubResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
