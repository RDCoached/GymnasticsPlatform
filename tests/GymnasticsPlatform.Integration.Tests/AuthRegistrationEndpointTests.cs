using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GymnasticsPlatform.Api.Models;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class AuthRegistrationEndpointTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Register_ValidRequest_ReturnsCreatedWithVerificationMessage()
    {
        // Arrange
        var client = factory.CreateClient();
        var request = new RegisterRequest(
            Email: "newuser@example.com",
            Password: "Test123!",
            FullName: "New User");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        result.Should().NotBeNull();
        result!.RequiresEmailVerification.Should().BeTrue();
        result.Message.Should().Contain("verification");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        // Arrange
        var client = factory.CreateClient();
        var request = new RegisterRequest(
            Email: "duplicate@example.com",
            Password: "Test123!",
            FullName: "Duplicate User");

        // First registration
        await client.PostAsJsonAsync("/api/auth/register", request);

        // Act - Try to register again with same email
        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var request = new RegisterRequest(
            Email: "not-an-email",
            Password: "Test123!",
            FullName: "Test User");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WeakPassword_ReturnsBadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var request = new RegisterRequest(
            Email: "user@example.com",
            Password: "weak",
            FullName: "Test User");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_MissingFullName_ReturnsBadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var request = new RegisterRequest(
            Email: "user@example.com",
            Password: "Test123!",
            FullName: "");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
