using Auth.Application.Services;
using Common.Core;

namespace GymnasticsPlatform.Api.Services;

/// <summary>
/// Test implementation of Keycloak Admin Service that bypasses Keycloak entirely.
/// ONLY used in E2E test mode - never in production.
/// </summary>
public sealed class TestKeycloakAdminService : IKeycloakAdminService
{
    public Task UpdateUserTenantIdAsync(string keycloakUserId, Guid newTenantId, CancellationToken ct = default)
    {
        // No-op in test mode
        return Task.CompletedTask;
    }

    public Task<Result<string>> CreateUserAsync(
        string email,
        string password,
        string fullName,
        Guid tenantId,
        CancellationToken ct = default)
    {
        // Return a fake Keycloak user ID based on email
        var fakeUserId = $"test-user-{Guid.NewGuid()}";
        return Task.FromResult(Result.Success(fakeUserId));
    }

    public Task<Result<TokenResponse>> AuthenticateAsync(
        string email,
        string password,
        string clientId,
        CancellationToken ct = default)
    {
        // Return a fake token for E2E tests
        var response = new TokenResponse(
            AccessToken: "fake-access-token",
            RefreshToken: "fake-refresh-token",
            ExpiresIn: 3600,
            TokenType: "Bearer"
        );
        return Task.FromResult(Result.Success(response));
    }

    public Task<Result<TokenResponse>> RefreshTokenAsync(
        string refreshToken,
        string clientId,
        CancellationToken ct = default)
    {
        // Return a fake refreshed token
        var response = new TokenResponse(
            AccessToken: "fake-refreshed-access-token",
            RefreshToken: refreshToken,
            ExpiresIn: 3600,
            TokenType: "Bearer"
        );
        return Task.FromResult(Result.Success(response));
    }

    public Task<Result> InitiatePasswordResetAsync(string email, CancellationToken ct = default)
    {
        // Success in test mode
        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendVerificationEmailAsync(string keycloakUserId, CancellationToken ct = default)
    {
        // Success in test mode
        return Task.FromResult(Result.Success());
    }

    public Task<Result<bool>> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        // Always return false (email doesn't exist) in test mode to allow registration
        return Task.FromResult(Result.Success(false));
    }
}
