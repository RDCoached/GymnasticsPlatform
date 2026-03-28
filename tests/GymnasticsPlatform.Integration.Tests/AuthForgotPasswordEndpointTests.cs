using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GymnasticsPlatform.Api.Models;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class AuthForgotPasswordEndpointTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task ForgotPassword_ExistingUser_ReturnsSuccess()
    {
        // Arrange
        var client = factory.CreateClient();
        var email = $"forgotpass-{Guid.NewGuid()}@example.com";

        // Register a user first
        var registerRequest = new RegisterRequest(
            Email: email,
            Password: "Test123!",
            FullName: "Forgot Password User");
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var forgotPasswordRequest = new ForgotPasswordRequest(
            Email: email);

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", forgotPasswordRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ForgotPasswordResponse>();
        result.Should().NotBeNull();
        result!.Message.Should().Contain("If an account exists");
    }

    [Fact]
    public async Task ForgotPassword_NonExistentUser_ReturnsSuccessWithoutRevealingExistence()
    {
        // Arrange
        var client = factory.CreateClient();
        var forgotPasswordRequest = new ForgotPasswordRequest(
            Email: "nonexistent@example.com");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", forgotPasswordRequest);

        // Assert - Should return success to prevent email enumeration
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ForgotPasswordResponse>();
        result.Should().NotBeNull();
        result!.Message.Should().Contain("If an account exists");
    }

    [Fact]
    public async Task ForgotPassword_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var forgotPasswordRequest = new ForgotPasswordRequest(
            Email: "not-an-email");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", forgotPasswordRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ForgotPassword_EmptyEmail_ReturnsBadRequest()
    {
        // Arrange
        var client = factory.CreateClient();
        var forgotPasswordRequest = new ForgotPasswordRequest(
            Email: "");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", forgotPasswordRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ForgotPassword_ResponseSameForExistingAndNonExistingEmails()
    {
        // Arrange
        var client = factory.CreateClient();
        var existingEmail = $"existing-{Guid.NewGuid()}@example.com";
        var nonExistingEmail = $"nonexisting-{Guid.NewGuid()}@example.com";

        // Register a user
        var registerRequest = new RegisterRequest(
            Email: existingEmail,
            Password: "Test123!",
            FullName: "Existing User");
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var existingEmailRequest = new ForgotPasswordRequest(
            Email: existingEmail);

        var nonExistingEmailRequest = new ForgotPasswordRequest(
            Email: nonExistingEmail);

        // Act
        var existingResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", existingEmailRequest);
        var nonExistingResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", nonExistingEmailRequest);

        // Assert - Both should return the same response to prevent email enumeration
        existingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        nonExistingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var existingResult = await existingResponse.Content.ReadFromJsonAsync<ForgotPasswordResponse>();
        var nonExistingResult = await nonExistingResponse.Content.ReadFromJsonAsync<ForgotPasswordResponse>();

        existingResult!.Message.Should().Be(nonExistingResult!.Message);
    }
}
