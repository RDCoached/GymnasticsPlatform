using System.Net;
using System.Net.Http.Json;
using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class GymnastEndpointsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private async Task<HttpClient> CreateCoachClientAsync()
    {
        var email = $"coach-{Guid.NewGuid()}@example.com";
        var client = factory.CreateClient();

        // Register user
        var registerRequest = new
        {
            Email = email,
            Password = "Test123!",
            FullName = "Test Coach"
        };
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        factory.MockAuthProvider.VerifyEmail(email);

        // Complete onboarding to get real tenant
        await Task.Delay(50);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var userProfile = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstAsync(u => u.Email == email);

        // Assign Coach role
        var roleService = scope.ServiceProvider.GetRequiredService<Auth.Application.Services.IRoleService>();
        await roleService.AssignRolesAsync(
            userProfile.TenantId,
            userProfile.ProviderUserId,
            [Role.Coach],
            "system",
            CancellationToken.None);

        // Set auth headers
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userProfile.ProviderUserId);
        client.DefaultRequestHeaders.Add("X-Test-Tenant-Id", userProfile.TenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Email", email);
        client.DefaultRequestHeaders.Add("X-Test-Username", "Test Coach");

        return client;
    }

    [Fact]
    public async Task ListGymnasts_AsCoach_ReturnsGymnastsInTenant()
    {
        // Arrange
        var client = await CreateCoachClientAsync();

        // Get tenant ID
        var tenantId = Guid.Parse(client.DefaultRequestHeaders.GetValues("X-Test-Tenant-Id").First());

        // Seed a gymnast in the same tenant
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var gymnastProfile = Auth.Domain.Entities.UserProfile.Create(
            tenantId,
            $"keycloak-{Guid.NewGuid()}",
            "gymnast@example.com",
            "Test Gymnast",
            DateTimeOffset.UtcNow);
        db.UserProfiles.Add(gymnastProfile);
        await db.SaveChangesAsync();

        var gymnastRole = UserRole.Create(
            tenantId,
            gymnastProfile.ProviderUserId,
            Role.Gymnast,
            "system",
            TimeProvider.System);
        db.UserRoles.Add(gymnastRole);
        await db.SaveChangesAsync();

        // Act
        var response = await client.GetAsync("/api/gymnasts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var gymnasts = await response.Content.ReadFromJsonAsync<List<GymnastResponse>>();
        gymnasts.Should().NotBeNull();
        gymnasts.Should().ContainSingle(g => g.Name == "Test Gymnast");
    }

    [Fact]
    public async Task CreateGymnast_AsCoach_CreatesGymnastWithRole()
    {
        // Arrange
        var client = await CreateCoachClientAsync();
        var request = new CreateGymnastRequest(
            Email: $"newgymnast-{Guid.NewGuid()}@example.com",
            FullName: "New Gymnast");

        // Act
        var response = await client.PostAsJsonAsync("/api/gymnasts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var gymnast = await response.Content.ReadFromJsonAsync<GymnastResponse>();
        gymnast.Should().NotBeNull();
        gymnast!.Name.Should().Be("New Gymnast");
        gymnast.Email.Should().Be(request.Email);

        // Verify role was assigned
        var tenantId = Guid.Parse(client.DefaultRequestHeaders.GetValues("X-Test-Tenant-Id").First());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var userRole = await db.UserRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ur => ur.TenantId == tenantId && ur.Role == Role.Gymnast);
        userRole.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateGymnast_AsCoach_UpdatesGymnastProfile()
    {
        // Arrange
        var client = await CreateCoachClientAsync();
        var tenantId = Guid.Parse(client.DefaultRequestHeaders.GetValues("X-Test-Tenant-Id").First());

        // Seed a gymnast
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var gymnastProfile = Auth.Domain.Entities.UserProfile.Create(
            tenantId,
            $"keycloak-{Guid.NewGuid()}",
            "updategymnast@example.com",
            "Original Name",
            DateTimeOffset.UtcNow);
        db.UserProfiles.Add(gymnastProfile);
        await db.SaveChangesAsync();

        var gymnastRole = UserRole.Create(
            tenantId,
            gymnastProfile.ProviderUserId,
            Role.Gymnast,
            "system",
            TimeProvider.System);
        db.UserRoles.Add(gymnastRole);
        await db.SaveChangesAsync();

        var updateRequest = new UpdateGymnastRequest("Updated Name");

        // Act
        var response = await client.PutAsJsonAsync($"/api/gymnasts/{gymnastProfile.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var gymnast = await response.Content.ReadFromJsonAsync<GymnastResponse>();
        gymnast.Should().NotBeNull();
        gymnast!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteGymnast_AsCoach_RemovesGymnast()
    {
        // Arrange
        var client = await CreateCoachClientAsync();
        var tenantId = Guid.Parse(client.DefaultRequestHeaders.GetValues("X-Test-Tenant-Id").First());

        // Seed a gymnast
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var gymnastProfile = Auth.Domain.Entities.UserProfile.Create(
            tenantId,
            $"keycloak-{Guid.NewGuid()}",
            "deletegymnast@example.com",
            "To Delete",
            DateTimeOffset.UtcNow);
        db.UserProfiles.Add(gymnastProfile);
        await db.SaveChangesAsync();

        var gymnastRole = UserRole.Create(
            tenantId,
            gymnastProfile.ProviderUserId,
            Role.Gymnast,
            "system",
            TimeProvider.System);
        db.UserRoles.Add(gymnastRole);
        await db.SaveChangesAsync();

        // Act
        var response = await client.DeleteAsync($"/api/gymnasts/{gymnastProfile.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify gymnast role was removed
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var remainingRole = await verifyDb.UserRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ur => ur.ProviderUserId == gymnastProfile.ProviderUserId && ur.Role == Role.Gymnast);
        remainingRole.Should().BeNull();
    }

    [Fact]
    public async Task ListGymnasts_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/gymnasts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // TODO: Re-enable after fixing TestWebApplicationFactory to support pgvector
    // [Fact]
    // public async Task ListGymnastsByCoach_ReturnsOnlyGymnastsWithProgrammes()
    // {
    //     // Test implementation commented out until TrainingDbContext migrations work in test container
    // }
}

public record CreateGymnastRequest(string Email, string FullName);
public record UpdateGymnastRequest(string FullName);
public record GymnastResponse(Guid Id, string Name, string Email);
