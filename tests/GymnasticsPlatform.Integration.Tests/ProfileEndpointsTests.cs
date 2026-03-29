using System.Net;
using System.Net.Http.Json;
using Auth.Infrastructure.Persistence;
using FluentAssertions;
using GymnasticsPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class ProfileEndpointsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string fullName)
    {
        var client = factory.CreateClient();

        // Register and verify user
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "Test123!",
            FullName: fullName);
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        factory.MockKeycloakService.VerifyEmail(email);

        // Get the Keycloak user ID from the database
        // Use a new scope to ensure we see the committed data
        await Task.Delay(50); // Small delay to ensure database write is committed

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var userProfile = await db.UserProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email);

        if (userProfile == null)
        {
            throw new InvalidOperationException($"User profile not found for email: {email}");
        }

        // Add test authentication headers
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userProfile.KeycloakUserId);
        client.DefaultRequestHeaders.Add("X-Test-Tenant-Id", userProfile.TenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Email", email);
        client.DefaultRequestHeaders.Add("X-Test-Username", fullName);

        return client;
    }

    [Fact]
    public async Task GetProfile_AuthenticatedUser_ReturnsProfile()
    {
        // Arrange
        var email = $"getprofile-{Guid.NewGuid()}@example.com";
        var fullName = "Get Profile Test";
        var client = await CreateAuthenticatedClientAsync(email, fullName);

        // Act
        var response = await client.GetAsync("/api/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        profile.Should().NotBeNull();
        profile!.Email.Should().Be(email);
        profile.FullName.Should().Be(fullName);
        profile.OnboardingCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetProfile_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_ValidRequest_UpdatesProfileAndReturnsNewData()
    {
        // Arrange
        var email = $"updateprofile-{Guid.NewGuid()}@example.com";
        var originalName = "Original Name";
        var updatedName = "Updated Name";
        var client = await CreateAuthenticatedClientAsync(email, originalName);

        var updateRequest = new UpdateProfileRequest(FullName: updatedName);

        // Act
        var response = await client.PutAsJsonAsync("/api/profile", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        profile.Should().NotBeNull();
        profile!.Email.Should().Be(email);
        profile.FullName.Should().Be(updatedName);

        // Verify the change persisted by fetching profile again
        var getResponse = await client.GetAsync("/api/profile");
        var fetchedProfile = await getResponse.Content.ReadFromJsonAsync<ProfileResponse>();
        fetchedProfile!.FullName.Should().Be(updatedName);
    }

    [Fact]
    public async Task UpdateProfile_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var email = $"emptyname-{Guid.NewGuid()}@example.com";
        var client = await CreateAuthenticatedClientAsync(email, "Test User");

        var updateRequest = new UpdateProfileRequest(FullName: "");

        // Act
        var response = await client.PutAsJsonAsync("/api/profile", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProfile_NameTooShort_ReturnsBadRequest()
    {
        // Arrange
        var email = $"shortname-{Guid.NewGuid()}@example.com";
        var client = await CreateAuthenticatedClientAsync(email, "Test User");

        var updateRequest = new UpdateProfileRequest(FullName: "A");

        // Act
        var response = await client.PutAsJsonAsync("/api/profile", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProfile_NameTooLong_ReturnsBadRequest()
    {
        // Arrange
        var email = $"longname-{Guid.NewGuid()}@example.com";
        var client = await CreateAuthenticatedClientAsync(email, "Test User");

        var updateRequest = new UpdateProfileRequest(FullName: new string('A', 101));

        // Act
        var response = await client.PutAsJsonAsync("/api/profile", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProfile_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();
        var updateRequest = new UpdateProfileRequest(FullName: "Test Name");

        // Act
        var response = await client.PutAsJsonAsync("/api/profile", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

public record ProfileResponse(string Email, string FullName, bool OnboardingCompleted);
public record UpdateProfileRequest(string FullName);
