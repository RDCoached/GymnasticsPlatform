using System.Net;
using System.Net.Http.Json;
using Auth.Application.Services;
using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using FluentAssertions;
using GymnasticsPlatform.Api.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class ClubInvitationTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly TimeProvider _clock = TimeProvider.System;

    public ClubInvitationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _factory.ResetMockServices();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SendEmailInvite_ValidRequest_CreatesInviteAndSendsEmail()
    {
        // Arrange
        var (client, clubId) = await CreateTestClubAsync();

        var request = new
        {
            Email = "newuser@example.com",
            InviteType = InviteType.Coach,
            Description = "Welcome to the team!"
        };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/invites/send-email",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var invite = await response.Content.ReadFromJsonAsync<EmailInviteResponse>();
        invite.Should().NotBeNull();
        invite!.Email.Should().Be("newuser@example.com");
        invite.Code.Should().HaveLength(8);
        invite.InviteType.Should().Be(InviteType.Coach);
        invite.Description.Should().Be("Welcome to the team!");
        invite.SentAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        // Verify email was sent
        var emailService = _factory.TestEmailService;
        emailService.SentEmails.Should().ContainSingle();
        var sentEmail = emailService.SentEmails.Single();
        sentEmail.ToEmail.Should().Be("newuser@example.com");
        sentEmail.InviteCode.Should().Be(invite.Code);
        sentEmail.InviteType.Should().Be(InviteType.Coach);
        sentEmail.InviteUrl.Should().Contain($"inviteCode={invite.Code}");
    }

    [Fact]
    public async Task SendEmailInvite_CreatesInviteWithMaxUsesOne()
    {
        // Arrange
        var (client, clubId) = await CreateTestClubAsync();

        var request = new
        {
            Email = "single-use@example.com",
            InviteType = InviteType.Gymnast,
            Description = (string?)null
        };

        // Act
        await client.PostAsJsonAsync($"/api/clubs/{clubId}/invites/send-email", request);

        // Assert - verify in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var invite = await db.ClubInvites
            .Where(i => i.Email == "single-use@example.com")
            .SingleAsync();

        invite.MaxUses.Should().Be(1);
        invite.TimesUsed.Should().Be(0);
        invite.IsSingleUse().Should().BeTrue();
    }

    [Fact]
    public async Task SendEmailInvite_DuplicateEmail_ReturnsConflict()
    {
        // Arrange
        var (client, clubId) = await CreateTestClubAsync();

        var request = new
        {
            Email = "duplicate@example.com",
            InviteType = InviteType.Gymnast,
            Description = (string?)null
        };

        // Send first invite
        await client.PostAsJsonAsync($"/api/clubs/{clubId}/invites/send-email", request);

        // Act - try sending duplicate
        var response = await client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/invites/send-email",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Active invitation already exists for this email");
    }

    [Fact]
    public async Task SendEmailInvite_UsedUpInviteExists_AllowsNewInvite()
    {
        // Arrange
        var (client, clubId) = await CreateTestClubAsync();

        // Create invite and mark it as used up
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var usedInvite = ClubInvite.Create(
                clubId,
                InviteType.Coach,
                maxUses: 1,
                expiresAt: _clock.GetUtcNow().AddDays(7),
                description: null,
                "used@example.com",
                _clock);

            usedInvite.MarkAsUsed(_clock); // Now it's at max uses
            db.ClubInvites.Add(usedInvite);
            await db.SaveChangesAsync();
        }

        var request = new
        {
            Email = "used@example.com",
            InviteType = InviteType.Gymnast,
            Description = (string?)null
        };

        // Act - Should allow new invite since old one is used up
        var response = await client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/invites/send-email",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SendEmailInvite_RateLimitExceeded_Returns429()
    {
        // Arrange
        var (client, clubId) = await CreateTestClubAsync();

        // Send 10 invites (rate limit)
        for (int i = 0; i < 10; i++)
        {
            var request = new
            {
                Email = $"user{i}@example.com",
                InviteType = InviteType.Gymnast,
                Description = (string?)null
            };

            var result = await client.PostAsJsonAsync(
                $"/api/clubs/{clubId}/invites/send-email",
                request);

            result.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Act - 11th should fail
        var response = await client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/invites/send-email",
            new
            {
                Email = "user11@example.com",
                InviteType = InviteType.Gymnast,
                Description = (string?)null
            });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Rate limit exceeded");
    }

    [Fact]
    public async Task SendEmailInvite_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var (client, clubId) = await CreateTestClubAsync();

        var request = new
        {
            Email = "not-an-email",
            InviteType = InviteType.Coach,
            Description = (string?)null
        };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/invites/send-email",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendEmailInvite_DescriptionTooLong_ReturnsBadRequest()
    {
        // Arrange
        var (client, clubId) = await CreateTestClubAsync();

        var request = new
        {
            Email = "test@example.com",
            InviteType = InviteType.Coach,
            Description = new string('x', 501) // Max is 500
        };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/invites/send-email",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendEmailInvite_InvalidInviteType_ReturnsBadRequest()
    {
        // Arrange
        var (client, clubId) = await CreateTestClubAsync();

        var request = new
        {
            Email = "test@example.com",
            InviteType = 999, // Invalid
            Description = (string?)null
        };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/invites/send-email",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendEmailInvite_NonExistentClub_ReturnsNotFound()
    {
        // Arrange
        var (client, _) = await CreateTestClubAsync();
        var nonExistentClubId = Guid.NewGuid();

        var request = new
        {
            Email = "test@example.com",
            InviteType = InviteType.Coach,
            Description = (string?)null
        };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/clubs/{nonExistentClubId}/invites/send-email",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SendEmailInvite_Unauthorized_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var clubId = Guid.NewGuid();

        var request = new
        {
            Email = "test@example.com",
            InviteType = InviteType.Coach,
            Description = (string?)null
        };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/invites/send-email",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListInvites_IncludesEmailInvites()
    {
        // Arrange
        var (client, clubId) = await CreateTestClubAsync();

        // Create email invite
        await client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/invites/send-email",
            new SendEmailInviteRequest("email-invite@example.com", InviteType.Coach, "Email invite"));

        // Act
        var response = await client.GetAsync($"/api/clubs/{clubId}/invites");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var invites = await response.Content.ReadFromJsonAsync<List<InviteResponse>>();
        invites.Should().NotBeNull();
        invites.Should().HaveCount(1);

        var emailInvite = invites!.Single();
        emailInvite.Email.Should().Be("email-invite@example.com");
        emailInvite.MaxUses.Should().Be(1);
        emailInvite.SentAt.Should().NotBeNull();
    }

    private async Task<(HttpClient client, Guid clubId)> CreateTestClubAsync()
    {
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        // Create user profile and assign ClubAdmin role
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();

            _factory.TestTenantContext.TenantId = tenantId;

            var userProfile = UserProfile.Create(
                tenantId,
                userId,
                "admin@example.com",
                "Admin User",
                _clock.GetUtcNow());
            db.UserProfiles.Add(userProfile);
            await db.SaveChangesAsync();

            await roleService.AssignRolesAsync(
                tenantId,
                userId,
                [Role.ClubAdmin],
                null,
                CancellationToken.None);
        }

        // Create club in this tenant
        Guid clubId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            _factory.TestTenantContext.TenantId = tenantId;

            var club = Club.Create("Test Club", userId, _clock);
            club.GetType().GetProperty("TenantId")!.SetValue(club, tenantId);
            db.Clubs.Add(club);
            await db.SaveChangesAsync();
            clubId = club.Id;
        }

        var client = _factory.CreateAuthenticatedUserClient(userId, tenantId);
        return (client, clubId);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var userId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        // Create user profile
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        _factory.TestTenantContext.TenantId = tenantId;

        var profile = UserProfile.Create(
            tenantId,
            userId,
            "test@example.com",
            "Test User",
            _clock.GetUtcNow());
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();

        return _factory.CreateAuthenticatedUserClient(userId, tenantId);
    }
}
