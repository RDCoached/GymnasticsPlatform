using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GymnasticsPlatform.Api.Models;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class AuthLoginEndpointTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokensAndUserInfo()
    {
        // Arrange
        var client = factory.CreateClient();
        var email = $"logintest-{Guid.NewGuid()}@example.com";

        // First register a user
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "Test123!",
            FullName: "Login Test");
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Verify email (simulate clicking verification link)
        factory.MockKeycloakService.VerifyEmail(email);

        var loginRequest = new LoginRequest(
            Email: email,
            Password: "Test123!");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.ExpiresIn.Should().BeGreaterThan(0);
        result.TokenType.Should().Be("Bearer");
        result.User.Should().NotBeNull();
        result.User.Email.Should().Be(email);
        result.User.FullName.Should().Be("Login Test");
    }

    [Fact]
    public async Task Login_UnverifiedEmail_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();
        var email = $"unverified-{Guid.NewGuid()}@example.com";

        // Register a user but don't verify email
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "Test123!",
            FullName: "Unverified User");
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest(
            Email: email,
            Password: "Test123!");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();
        var email = $"wrongpass-{Guid.NewGuid()}@example.com";

        // Register and verify a user
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "Test123!",
            FullName: "Wrong Pass User");
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        factory.MockKeycloakService.VerifyEmail(email);

        var loginRequest = new LoginRequest(
            Email: email,
            Password: "WrongPassword123!");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_NonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();
        var email = $"nonexistent-{Guid.NewGuid()}@example.com";

        var loginRequest = new LoginRequest(
            Email: email,
            Password: "Test123!");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var loginRequest = new LoginRequest(
            Email: "not-an-email",
            Password: "Test123!");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_EmptyPassword_ReturnsBadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var loginRequest = new LoginRequest(
            Email: "test@example.com",
            Password: "");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
