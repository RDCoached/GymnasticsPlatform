using Auth.Application.Services;
using Common.Core;

namespace GymnasticsPlatform.Integration.Tests.Mocks;

/// <summary>
/// Mock implementation of IKeycloakAdminService for integration testing.
/// Simulates Keycloak behavior without requiring a real Keycloak instance.
/// </summary>
public sealed class MockKeycloakAdminService : IKeycloakAdminService
{
    private readonly HashSet<string> _registeredEmails = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _userCredentials = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _emailVerificationStatus = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _refreshTokens = new();

    public Task UpdateUserTenantIdAsync(string providerUserId, Guid newTenantId, CancellationToken ct = default)
    {
        // For testing, just succeed
        return Task.CompletedTask;
    }

    public Task<Result<string>> CreateUserAsync(
        string email,
        string password,
        string fullName,
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (_registeredEmails.Contains(email))
        {
            return Task.FromResult(Result.Failure<string>(
                ErrorType.Conflict,
                "User with this email already exists"));
        }

        var userId = Guid.NewGuid().ToString();
        _registeredEmails.Add(email);
        _userCredentials[email] = password;
        _emailVerificationStatus[email] = false; // Starts unverified

        return Task.FromResult(Result.Success(userId));
    }

    public Task<Result<TokenResponse>> AuthenticateAsync(
        string email,
        string password,
        string clientId,
        CancellationToken ct = default)
    {
        // Check if user exists
        if (!_userCredentials.ContainsKey(email))
        {
            return Task.FromResult(Result.Failure<TokenResponse>(
                ErrorType.Unauthorized,
                "Invalid credentials"));
        }

        // Check if email is verified
        if (!_emailVerificationStatus[email])
        {
            return Task.FromResult(Result.Failure<TokenResponse>(
                ErrorType.Unauthorized,
                "Email not verified. Please check your email for verification link."));
        }

        // Check password
        if (_userCredentials[email] != password)
        {
            return Task.FromResult(Result.Failure<TokenResponse>(
                ErrorType.Unauthorized,
                "Invalid credentials"));
        }

        // Generate tokens
        var accessToken = $"mock-access-token-{Guid.NewGuid()}";
        var refreshToken = $"mock-refresh-token-{Guid.NewGuid()}";

        _refreshTokens[refreshToken] = email;

        var tokenResponse = new TokenResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresIn: 3600,
            TokenType: "Bearer");

        return Task.FromResult(Result.Success(tokenResponse));
    }

    public Task<Result<TokenResponse>> RefreshTokenAsync(
        string refreshToken,
        string clientId,
        CancellationToken ct = default)
    {
        if (!_refreshTokens.ContainsKey(refreshToken))
        {
            return Task.FromResult(Result.Failure<TokenResponse>(
                ErrorType.Unauthorized,
                "Invalid or expired refresh token"));
        }

        // Generate new tokens
        var newAccessToken = $"mock-access-token-{Guid.NewGuid()}";
        var newRefreshToken = $"mock-refresh-token-{Guid.NewGuid()}";

        var email = _refreshTokens[refreshToken];
        _refreshTokens.Remove(refreshToken);
        _refreshTokens[newRefreshToken] = email;

        var tokenResponse = new TokenResponse(
            AccessToken: newAccessToken,
            RefreshToken: newRefreshToken,
            ExpiresIn: 3600,
            TokenType: "Bearer");

        return Task.FromResult(Result.Success(tokenResponse));
    }

    public Task<Result> InitiatePasswordResetAsync(string email, CancellationToken ct = default)
    {
        // Always return success to prevent email enumeration
        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendVerificationEmailAsync(string providerUserId, CancellationToken ct = default)
    {
        // For testing, just succeed
        return Task.FromResult(Result.Success());
    }

    public Task<Result<bool>> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        var exists = _registeredEmails.Contains(email);
        return Task.FromResult(Result.Success(exists));
    }

    // Test helper methods
    public void VerifyEmail(string email)
    {
        if (_emailVerificationStatus.ContainsKey(email))
        {
            _emailVerificationStatus[email] = true;
        }
    }

    public void Reset()
    {
        _registeredEmails.Clear();
        _userCredentials.Clear();
        _emailVerificationStatus.Clear();
        _refreshTokens.Clear();
    }
}
