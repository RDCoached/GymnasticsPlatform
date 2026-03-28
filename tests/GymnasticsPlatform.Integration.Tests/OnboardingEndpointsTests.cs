using System.Net;
using System.Net.Http.Json;
using Auth.Infrastructure.Persistence;
using FluentAssertions;
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

    [Fact]
    public async Task GetOnboardingStatus_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/onboarding/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    // TODO: Add authenticated tests after authentication mocking is set up
    // These tests verify endpoints exist and require authentication
    // Full business logic tests will be added with proper auth mocking
}
