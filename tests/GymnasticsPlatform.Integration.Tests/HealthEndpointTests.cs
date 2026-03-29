using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class HealthEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task HealthEndpoint_IsAccessibleWithoutAuthentication()
    {
        // Arrange - Create a client without any authentication headers

        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().NotContainKey("WWW-Authenticate");
    }
}
