using System.Net;
using System.Net.Http.Json;
using Auth.Domain.Entities;
using FluentAssertions;
using GymnasticsPlatform.Integration.Tests.Mocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Sdk;

namespace GymnasticsPlatform.Integration.Tests.DomainEvents;

public sealed class UserTenantUpdatedIntegrationTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UserTenantUpdatedIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _factory.ResetDatabaseAsync();
        _factory.ResetMockAuthProvider();
    }

    [Fact]
    public async Task UpdateTenant_PublishesEvent_AndHandlerCallsAuthProvider()
    {
        // Arrange
        var oldTenantId = Guid.NewGuid();
        var newTenantId = Guid.NewGuid();
        var providerUserId = "entra-user-123";
        var email = "test@example.com";
        var fullName = "Test User";

        // Create a user profile directly in the database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Auth.Infrastructure.Persistence.AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var userProfile = UserProfile.Create(
                oldTenantId,
                providerUserId,
                email,
                fullName,
                clock.GetUtcNow());
            db.UserProfiles.Add(userProfile);
            await db.SaveChangesAsync();
        }

        // Get reference to mock auth provider to verify it was called
        var mockAuthProvider = _factory.GetMockAuthProvider();
        mockAuthProvider.Reset(); // Clear any previous calls

        // Act - Update the user's tenant via the service
        using (var scope = _factory.Services.CreateScope())
        {
            var userTenantService = scope.ServiceProvider.GetRequiredService<Auth.Application.Services.IUserTenantService>();
            await userTenantService.UpdateUserTenantAsync(providerUserId, newTenantId);
        }

        // Assert - Poll to verify the async event handler executed
        await AssertWithPollingAsync(
            () => mockAuthProvider.UpdateTenantIdCallCount > 0,
            "Domain event handler should have called UpdateUserTenantIdAsync",
            timeout: TimeSpan.FromSeconds(5));

        mockAuthProvider.UpdateTenantIdCallCount.Should().Be(1);
        mockAuthProvider.LastUpdatedProviderUserId.Should().Be(providerUserId);
        mockAuthProvider.LastUpdatedTenantId.Should().Be(newTenantId);

        // Verify database was updated
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Auth.Infrastructure.Persistence.AuthDbContext>();
            var updatedProfile = await db.UserProfiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.ProviderUserId == providerUserId);

            updatedProfile.Should().NotBeNull();
            updatedProfile!.TenantId.Should().Be(newTenantId);
        }
    }

    [Fact]
    public async Task CreateClub_UpdatesTenantId_PublishesEvent_AndHandlerCallsAuthProvider()
    {
        // Arrange
        var onboardingTenantId = new Guid("00000000-0000-0000-0000-000000000001");
        var providerUserId = "entra-user-456";
        var email = "clubowner@example.com";
        var fullName = "Club Owner";
        var clubName = "Test Gymnastics Club";

        // Create user in onboarding tenant
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Auth.Infrastructure.Persistence.AuthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var userProfile = UserProfile.Create(
                onboardingTenantId,
                providerUserId,
                email,
                fullName,
                clock.GetUtcNow());
            db.UserProfiles.Add(userProfile);
            await db.SaveChangesAsync();
        }

        var mockAuthProvider = _factory.GetMockAuthProvider();
        mockAuthProvider.Reset();

        // Act - Create club via onboarding endpoint
        _client.DefaultRequestHeaders.Add("X-User-Id", providerUserId);
        _client.DefaultRequestHeaders.Add("X-User-Email", email);

        var createClubRequest = new { Name = clubName };
        var response = await _client.PostAsJsonAsync("/api/onboarding/create-club", createClubRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Poll to verify the async event handler executed
        await AssertWithPollingAsync(
            () => mockAuthProvider.UpdateTenantIdCallCount > 0,
            "Domain event handler should have called UpdateUserTenantIdAsync after club creation",
            timeout: TimeSpan.FromSeconds(5));

        mockAuthProvider.UpdateTenantIdCallCount.Should().Be(1);
        mockAuthProvider.LastUpdatedProviderUserId.Should().Be(providerUserId);
        mockAuthProvider.LastUpdatedTenantId.Should().NotBe(onboardingTenantId);
        mockAuthProvider.LastUpdatedTenantId.Should().NotBeEmpty();

        // Verify user was moved out of onboarding tenant
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Auth.Infrastructure.Persistence.AuthDbContext>();
            var updatedProfile = await db.UserProfiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.ProviderUserId == providerUserId);

            updatedProfile.Should().NotBeNull();
            updatedProfile!.TenantId.Should().NotBe(onboardingTenantId);
        }
    }

    private static async Task AssertWithPollingAsync(
        Func<bool> condition,
        string errorMessage,
        TimeSpan timeout,
        TimeSpan? pollingInterval = null)
    {
        var interval = pollingInterval ?? TimeSpan.FromMilliseconds(100);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(interval);
        }

        throw new XunitException($"{errorMessage} (waited {timeout.TotalSeconds}s)");
    }
}
