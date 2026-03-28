using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class GlobalExceptionHandlerTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task UnhandledException_ReturnsProblemDetails()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/test/throw");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(500);
        problemDetails.Title.Should().Be("An error occurred while processing your request.");
        problemDetails.Type.Should().Be("https://tools.ietf.org/html/rfc9457#section-8.6");
        problemDetails.Detail.Should().NotContain("at "); // No stack trace
    }

    [Fact]
    public async Task UnhandledException_LogsErrorSecurely()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/test/throw");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        // Logging verification would require access to test logs
        // For now, we verify the response doesn't leak sensitive data
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("Exception");
        content.Should().NotContain("StackTrace");
    }

    [Fact]
    public async Task ArgumentException_ReturnsBadRequest()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/test/throw-argument");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Title.Should().Be("Bad Request");
    }

    [Fact]
    public async Task UnauthorizedAccessException_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/test/throw-unauthorized");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(401);
        problemDetails.Title.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task InvalidOperationException_ReturnsConflict()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/test/throw-invalid-operation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(409);
        problemDetails.Title.Should().Be("Conflict");
    }

    [Fact]
    public async Task NormalEndpoint_WorksCorrectly()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
