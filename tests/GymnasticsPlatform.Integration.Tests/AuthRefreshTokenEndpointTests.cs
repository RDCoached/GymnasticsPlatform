using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GymnasticsPlatform.Api.Models;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class AuthRefreshTokenEndpointTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task RefreshToken_ValidToken_ReturnsNewTokens()
    {
        // Arrange
        factory.ResetMockServices(); // Reset state from previous tests
        var client = factory.CreateClient();
        var email = $"refreshtest-{Guid.NewGuid()}@example.com";

        // Register, verify, and login to get a refresh token
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "Test123!",
            FullName: "Refresh Test");
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        factory.MockAuthProvider.VerifyEmail(email);

        var loginRequest = new LoginRequest(
            Email: email,
            Password: "Test123!");
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        loginResult.Should().NotBeNull();

        var refreshRequest = new RefreshTokenRequest(
            RefreshToken: loginResult!.RefreshToken);

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.ExpiresIn.Should().BeGreaterThan(0);
        result.TokenType.Should().Be("Bearer");

        // New tokens should be different from original
        result.AccessToken.Should().NotBe(loginResult.AccessToken);
        result.RefreshToken.Should().NotBe(loginResult.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_InvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        factory.ResetMockServices();
        var client = factory.CreateClient();
        var refreshRequest = new RefreshTokenRequest(
            RefreshToken: "invalid-refresh-token");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_EmptyToken_ReturnsBadRequest()
    {
        // Arrange
        factory.ResetMockServices();
        var client = factory.CreateClient();
        var refreshRequest = new RefreshTokenRequest(
            RefreshToken: "");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshToken_UsedTokenTwice_SecondRequestFails()
    {
        // Arrange
        factory.ResetMockServices();
        var client = factory.CreateClient();
        var email = $"twicetest-{Guid.NewGuid()}@example.com";

        // Register, verify, and login
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "Test123!",
            FullName: "Twice Test");
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        factory.MockAuthProvider.VerifyEmail(email);

        var loginRequest = new LoginRequest(
            Email: email,
            Password: "Test123!");
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        loginResult.Should().NotBeNull();

        var refreshRequest = new RefreshTokenRequest(
            RefreshToken: loginResult!.RefreshToken);

        // Act - Use token first time
        var firstResponse = await client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Try to use the same token again
        var secondResponse = await client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert - Second use should fail
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
