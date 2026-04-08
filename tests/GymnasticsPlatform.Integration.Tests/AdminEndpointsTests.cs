using System.Net;
using System.Net.Http.Json;
using Auth.Infrastructure.Persistence;
using FluentAssertions;
using GymnasticsPlatform.Api.Endpoints;
using GymnasticsPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class AdminEndpointsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private async Task<(HttpClient client, string userId)> CreateAdminClientAsync()
    {
        var client = factory.CreateClient();
        var email = $"admin-{Guid.NewGuid()}@example.com";
        var fullName = "Admin User";

        // Register and verify admin user
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "Admin123!",
            FullName: fullName);
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        factory.MockAuthProvider.VerifyEmail(email);

        // Get user ID from database
        await Task.Delay(50);
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

        // Add admin authentication headers with platform_admin role
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userProfile.ProviderUserId);
        client.DefaultRequestHeaders.Add("X-Test-Tenant-Id", userProfile.TenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Email", email);
        client.DefaultRequestHeaders.Add("X-Test-Username", fullName);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "platform_admin");

        return (client, userProfile.ProviderUserId);
    }

    private async Task<string> CreateTestUserAsync()
    {
        var client = factory.CreateClient();
        var email = $"testuser-{Guid.NewGuid()}@example.com";

        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "Test123!",
            FullName: "Test User");
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);

        await Task.Delay(50);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var userProfile = await db.UserProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email);

        return userProfile!.ProviderUserId;
    }

    [Fact]
    public async Task ListUsers_AuthenticatedAdmin_ReturnsUserList()
    {
        // Arrange
        var (client, _) = await CreateAdminClientAsync();

        // Create some test users
        await CreateTestUserAsync();
        await CreateTestUserAsync();

        // Act
        var response = await client.GetAsync("/api/admin/users?page=1&pageSize=20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListUsers_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/admin/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListUsers_NonAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = factory.CreateClient();
        var email = $"regular-{Guid.NewGuid()}@example.com";

        // Register regular user (no admin role)
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "Test123!",
            FullName: "Regular User");
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);

        await Task.Delay(50);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var userProfile = await db.UserProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email);

        // Add authentication headers without admin role
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userProfile!.ProviderUserId);
        client.DefaultRequestHeaders.Add("X-Test-Tenant-Id", userProfile.TenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Email", email);
        client.DefaultRequestHeaders.Add("X-Test-Username", "Regular User");
        // No X-Test-Roles header - no admin role

        // Act
        var response = await client.GetAsync("/api/admin/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

}
