using Common.Core;

namespace Auth.Application.Services;

/// <summary>
/// Abstraction for external authentication providers (Keycloak, Microsoft Entra ID, etc.)
/// Decouples the application layer from provider-specific implementations.
/// </summary>
public interface IAuthenticationProvider
{
    /// <summary>
    /// Creates a new user in the authentication provider.
    /// </summary>
    /// <param name="email">User's email address (used as username)</param>
    /// <param name="password">User's password (for email/password auth)</param>
    /// <param name="fullName">User's display name</param>
    /// <param name="tenantId">Initial tenant ID (typically OnboardingTenantId)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the provider's user ID on success</returns>
    Task<Result<string>> CreateUserAsync(
        string email,
        string password,
        string fullName,
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if an email address is already registered in the provider.
    /// </summary>
    /// <param name="email">Email address to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing true if email exists, false otherwise</returns>
    Task<Result<bool>> EmailExistsAsync(
        string email,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves user information from the provider.
    /// </summary>
    /// <param name="providerUserId">Provider-specific user ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing user info if found, null otherwise</returns>
    Task<Result<ProviderUserInfo?>> GetProviderUserInfoAsync(
        string providerUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Authenticates a user with email and password.
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="password">User's password</param>
    /// <param name="clientId">OAuth client ID for token audience</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing authentication result with tokens</returns>
    Task<Result<AuthenticationResult>> AuthenticateAsync(
        string email,
        string password,
        string clientId,
        CancellationToken ct = default);

    /// <summary>
    /// Exchanges an OAuth authorization code for access and refresh tokens.
    /// </summary>
    /// <param name="code">Authorization code from OAuth callback</param>
    /// <param name="redirectUri">Redirect URI used in the OAuth flow</param>
    /// <param name="clientId">Client ID that initiated the OAuth flow</param>
    /// <param name="codeVerifier">PKCE code verifier (required for SPAs)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing authentication result with tokens</returns>
    Task<Result<AuthenticationResult>> ExchangeCodeForTokensAsync(
        string code,
        string redirectUri,
        string clientId,
        string? codeVerifier = null,
        CancellationToken ct = default);

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    /// <param name="refreshToken">Valid refresh token</param>
    /// <param name="clientId">OAuth client ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing new authentication result with tokens</returns>
    Task<Result<AuthenticationResult>> RefreshTokenAsync(
        string refreshToken,
        string clientId,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a verification email to the user.
    /// </summary>
    /// <param name="providerUserId">Provider-specific user ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> SendVerificationEmailAsync(
        string providerUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Initiates a password reset flow for the user.
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> InitiatePasswordResetAsync(
        string email,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the tenant ID attribute in the provider's user profile.
    /// This is critical for multi-tenancy - the tenant ID is embedded in JWT tokens.
    /// </summary>
    /// <param name="providerUserId">Provider-specific user ID</param>
    /// <param name="newTenantId">New tenant ID to set</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> UpdateUserTenantIdAsync(
        string providerUserId,
        Guid newTenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Result of a successful authentication operation.
/// </summary>
public sealed record AuthenticationResult(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType,
    string ProviderUserId);

/// <summary>
/// Basic user information retrieved from the authentication provider.
/// </summary>
public sealed record ProviderUserInfo(
    string ProviderUserId,
    string Email,
    string? FullName);
