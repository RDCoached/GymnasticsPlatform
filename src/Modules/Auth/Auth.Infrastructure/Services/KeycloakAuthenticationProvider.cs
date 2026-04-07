using Auth.Application.Services;
using Common.Core;
using Microsoft.Extensions.Logging;

namespace Auth.Infrastructure.Services;

/// <summary>
/// Adapter that wraps KeycloakAdminService to implement the IAuthenticationProvider abstraction.
/// This maintains backward compatibility while decoupling the application layer from Keycloak.
/// </summary>
public sealed class KeycloakAuthenticationProvider(
    IKeycloakAdminService keycloakAdminService,
    ILogger<KeycloakAuthenticationProvider> logger) : IAuthenticationProvider
{
    public async Task<Result<string>> CreateUserAsync(
        string email,
        string password,
        string fullName,
        Guid tenantId,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Creating user in Keycloak: Email={Email}, TenantId={TenantId}",
            email,
            tenantId);

        var result = await keycloakAdminService.CreateUserAsync(
            email,
            password,
            fullName,
            tenantId,
            ct);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "Successfully created user in Keycloak: ProviderUserId={ProviderUserId}",
                result.Value);
        }
        else
        {
            logger.LogWarning(
                "Failed to create user in Keycloak: Email={Email}, Error={Error}",
                email,
                result.ErrorMessage);
        }

        return result;
    }

    public async Task<Result<bool>> EmailExistsAsync(
        string email,
        CancellationToken ct = default)
    {
        return await keycloakAdminService.EmailExistsAsync(email, ct);
    }

    public async Task<Result<ProviderUserInfo?>> GetProviderUserInfoAsync(
        string providerUserId,
        CancellationToken ct = default)
    {
        // Keycloak doesn't have a direct "get user info" endpoint in the current implementation
        // For now, return null to indicate the user info cannot be retrieved
        // If needed in the future, this can be implemented using the Keycloak Admin API
        logger.LogWarning(
            "GetUserInfoAsync not implemented for Keycloak provider. ProviderUserId={ProviderUserId}",
            providerUserId);

        return Result.Success<ProviderUserInfo?>(null);
    }

    public async Task<Result<AuthenticationResult>> AuthenticateAsync(
        string email,
        string password,
        string clientId,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Authenticating user via Keycloak: Email={Email}",
            email);

        var result = await keycloakAdminService.AuthenticateAsync(
            email,
            password,
            clientId,
            ct);

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                "Authentication failed for user: Email={Email}, Error={Error}",
                email,
                result.ErrorMessage);
            return Result.Failure<AuthenticationResult>(result.ErrorType ?? Common.Core.ErrorType.Internal, result.ErrorMessage ?? "Authentication failed");
        }

        var tokenResponse = result.Value!;

        // Convert TokenResponse to AuthenticationResult
        // Note: Keycloak's token response doesn't include the user ID directly
        // The user ID must be extracted from the JWT token or retrieved separately
        // For now, we'll use an empty string and let the caller extract it from the token
        var authResult = new AuthenticationResult(
            tokenResponse.AccessToken,
            tokenResponse.RefreshToken,
            tokenResponse.ExpiresIn,
            tokenResponse.TokenType,
            ProviderUserId: string.Empty); // Will be populated from JWT claims by caller

        logger.LogInformation(
            "Successfully authenticated user via Keycloak: Email={Email}",
            email);

        return Result.Success(authResult);
    }

    public async Task<Result<AuthenticationResult>> RefreshTokenAsync(
        string refreshToken,
        string clientId,
        CancellationToken ct = default)
    {
        logger.LogDebug("Refreshing token via Keycloak");

        var result = await keycloakAdminService.RefreshTokenAsync(
            refreshToken,
            clientId,
            ct);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Token refresh failed: Error={Error}", result.ErrorMessage);
            return Result.Failure<AuthenticationResult>(result.ErrorType ?? Common.Core.ErrorType.Internal, result.ErrorMessage ?? "Authentication failed");
        }

        var tokenResponse = result.Value!;

        var authResult = new AuthenticationResult(
            tokenResponse.AccessToken,
            tokenResponse.RefreshToken,
            tokenResponse.ExpiresIn,
            tokenResponse.TokenType,
            ProviderUserId: string.Empty); // Will be populated from JWT claims by caller

        logger.LogDebug("Successfully refreshed token via Keycloak");

        return Result.Success(authResult);
    }

    public async Task<Result> SendVerificationEmailAsync(
        string providerUserId,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Sending verification email via Keycloak: ProviderUserId={ProviderUserId}",
            providerUserId);

        return await keycloakAdminService.SendVerificationEmailAsync(providerUserId, ct);
    }

    public async Task<Result> InitiatePasswordResetAsync(
        string email,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Initiating password reset via Keycloak: Email={Email}",
            email);

        return await keycloakAdminService.InitiatePasswordResetAsync(email, ct);
    }

    public async Task<Result> UpdateUserTenantIdAsync(
        string providerUserId,
        Guid newTenantId,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Updating tenant ID via Keycloak: ProviderUserId={ProviderUserId}, NewTenantId={NewTenantId}",
            providerUserId,
            newTenantId);

        try
        {
            await keycloakAdminService.UpdateUserTenantIdAsync(providerUserId, newTenantId, ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to update tenant ID via Keycloak: ProviderUserId={ProviderUserId}",
                providerUserId);
            return Result.Failure(ErrorType.Internal, "Failed to update tenant ID");
        }
    }
}
