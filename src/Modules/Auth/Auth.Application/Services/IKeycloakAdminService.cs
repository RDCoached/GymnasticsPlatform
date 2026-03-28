using Common.Core;

namespace Auth.Application.Services;

public interface IKeycloakAdminService
{
    Task UpdateUserTenantIdAsync(string keycloakUserId, Guid newTenantId, CancellationToken ct = default);

    Task<Result<string>> CreateUserAsync(
        string email,
        string password,
        string fullName,
        Guid tenantId,
        CancellationToken ct = default);

    Task<Result<TokenResponse>> AuthenticateAsync(
        string email,
        string password,
        string clientId,
        CancellationToken ct = default);

    Task<Result<TokenResponse>> RefreshTokenAsync(
        string refreshToken,
        string clientId,
        CancellationToken ct = default);

    Task<Result> InitiatePasswordResetAsync(
        string email,
        CancellationToken ct = default);

    Task<Result> SendVerificationEmailAsync(
        string keycloakUserId,
        CancellationToken ct = default);

    Task<Result<bool>> EmailExistsAsync(
        string email,
        CancellationToken ct = default);
}

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType);
