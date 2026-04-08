using Auth.Application.Services;
using Azure.Identity;
using Common.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace Auth.Infrastructure.Services;

/// <summary>
/// Authentication provider for Microsoft Entra External ID (CIAM).
/// Handles user management via Microsoft Graph API and OAuth token flows.
/// </summary>
public sealed class ExternalIdAuthenticationProvider(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<ExternalIdAuthenticationProvider> logger) : IAuthenticationProvider
{
    private readonly string _tenantId = configuration["Authentication:ExternalId:TenantId"]
        ?? throw new InvalidOperationException("ExternalId TenantId not configured");
    private readonly string _apiClientId = configuration["Authentication:ExternalId:ApiClientId"]
        ?? throw new InvalidOperationException("ExternalId ApiClientId not configured");
    private readonly string _apiClientSecret = configuration["Authentication:ExternalId:ApiClientSecret"]
        ?? throw new InvalidOperationException("ExternalId ApiClientSecret not configured");
    private readonly string _authority = configuration["Authentication:ExternalId:Authority"]
        ?? $"https://login.microsoftonline.com/{configuration["Authentication:ExternalId:TenantId"]}";

    private GraphServiceClient CreateGraphClient()
    {
        var credential = new ClientSecretCredential(
            _tenantId,
            _apiClientId,
            _apiClientSecret);

        return new GraphServiceClient(credential);
    }

    public async Task<Result<string>> CreateUserAsync(
        string email,
        string password,
        string fullName,
        Guid tenantId,
        CancellationToken ct = default)
    {
        try
        {
            var graphClient = CreateGraphClient();

            var user = new User
            {
                AccountEnabled = true,
                DisplayName = fullName,
                MailNickname = email.Split('@')[0],
                UserPrincipalName = email,
                Mail = email,
                PasswordProfile = new PasswordProfile
                {
                    ForceChangePasswordNextSignIn = false,
                    Password = password
                },
                AdditionalData = new Dictionary<string, object>
                {
                    ["extension_tenant_id"] = tenantId.ToString()
                }
            };

            var createdUser = await graphClient.Users.PostAsync(user, cancellationToken: ct);

            if (createdUser?.Id is null)
            {
                logger.LogError("Failed to create user in External ID - no user ID returned");
                return Result<string>.Failure(ErrorType.Internal, "Failed to create user");
            }

            logger.LogInformation("Created user {Email} in External ID with ID {UserId}", email, createdUser.Id);
            return Result<string>.Success(createdUser.Id);
        }
        catch (ServiceException ex) when (ex.Message.Contains("already exists"))
        {
            logger.LogWarning("User {Email} already exists in External ID", email);
            return Result<string>.Failure(ErrorType.Conflict, "Email already registered");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create user {Email} in External ID", email);
            return Result<string>.Failure(ErrorType.Internal, "Failed to create user");
        }
    }

    public async Task<Result<bool>> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        try
        {
            var graphClient = CreateGraphClient();
            var users = await graphClient.Users
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = $"userPrincipalName eq '{email}' or mail eq '{email}'";
                    config.QueryParameters.Select = ["id"];
                }, cancellationToken: ct);

            var exists = users?.Value?.Count > 0;
            return Result<bool>.Success(exists);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check if email {Email} exists in External ID", email);
            return Result<bool>.Failure(ErrorType.Internal, "Failed to check email");
        }
    }

    public async Task<Result<ProviderUserInfo?>> GetProviderUserInfoAsync(
        string providerUserId,
        CancellationToken ct = default)
    {
        try
        {
            var graphClient = CreateGraphClient();
            var user = await graphClient.Users[providerUserId]
                .GetAsync(config =>
                {
                    config.QueryParameters.Select = ["id", "mail", "displayName", "userPrincipalName"];
                }, cancellationToken: ct);

            if (user is null)
            {
                return Result<ProviderUserInfo?>.Success(null);
            }

            var userInfo = new ProviderUserInfo(
                ProviderUserId: user.Id ?? providerUserId,
                Email: user.Mail ?? user.UserPrincipalName ?? "",
                FullName: user.DisplayName);

            return Result<ProviderUserInfo?>.Success(userInfo);
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == (int)System.Net.HttpStatusCode.NotFound)
        {
            return Result<ProviderUserInfo?>.Success(null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get user info for {ProviderUserId} from External ID", providerUserId);
            return Result<ProviderUserInfo?>.Failure(ErrorType.Internal, "Failed to retrieve user info");
        }
    }

    public async Task<Result<AuthenticationResult>> AuthenticateAsync(
        string email,
        string password,
        string clientId,
        CancellationToken ct = default)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            var tokenEndpoint = $"{_authority}/oauth2/v2.0/token";

            var requestBody = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = clientId,
                ["client_secret"] = _apiClientSecret,
                ["scope"] = $"api://{_tenantId}/gymnastics-api/user.access openid profile email offline_access",
                ["username"] = email,
                ["password"] = password
            };

            var response = await httpClient.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(requestBody),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Authentication failed for {Email}: {Error}", email, errorContent);
                return Result<AuthenticationResult>.Failure(ErrorType.Unauthorized, "Invalid credentials");
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);

            if (tokenResponse is null)
            {
                logger.LogError("Failed to parse token response for {Email}", email);
                return Result<AuthenticationResult>.Failure(ErrorType.Internal, "Authentication failed");
            }

            // Extract user ID from access token (decoded JWT claims)
            var providerUserId = ExtractUserIdFromToken(tokenResponse.AccessToken);

            var result = new AuthenticationResult(
                AccessToken: tokenResponse.AccessToken,
                RefreshToken: tokenResponse.RefreshToken ?? "",
                ExpiresIn: tokenResponse.ExpiresIn,
                TokenType: tokenResponse.TokenType,
                ProviderUserId: providerUserId);

            return Result<AuthenticationResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to authenticate user {Email}", email);
            return Result<AuthenticationResult>.Failure(ErrorType.Internal, "Authentication failed");
        }
    }

    public async Task<Result<AuthenticationResult>> ExchangeCodeForTokensAsync(
        string code,
        string redirectUri,
        string clientId,
        CancellationToken ct = default)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            var tokenEndpoint = $"{_authority}/oauth2/v2.0/token";

            var requestBody = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["scope"] = $"api://{_apiClientId}/user.access openid profile email offline_access"
            };

            var response = await httpClient.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(requestBody),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Token exchange failed: {Error}", errorContent);
                return Result<AuthenticationResult>.Failure(ErrorType.Unauthorized, "Invalid authorization code");
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);

            if (tokenResponse is null)
            {
                logger.LogError("Failed to parse token response");
                return Result<AuthenticationResult>.Failure(ErrorType.Internal, "Token exchange failed");
            }

            var providerUserId = ExtractUserIdFromToken(tokenResponse.AccessToken);

            var result = new AuthenticationResult(
                AccessToken: tokenResponse.AccessToken,
                RefreshToken: tokenResponse.RefreshToken ?? "",
                ExpiresIn: tokenResponse.ExpiresIn,
                TokenType: tokenResponse.TokenType,
                ProviderUserId: providerUserId);

            return Result<AuthenticationResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to exchange code for tokens");
            return Result<AuthenticationResult>.Failure(ErrorType.Internal, "Token exchange failed");
        }
    }

    public async Task<Result<AuthenticationResult>> RefreshTokenAsync(
        string refreshToken,
        string clientId,
        CancellationToken ct = default)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            var tokenEndpoint = $"{_authority}/oauth2/v2.0/token";

            var requestBody = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["client_secret"] = _apiClientSecret,
                ["refresh_token"] = refreshToken,
                ["scope"] = $"api://{_apiClientId}/user.access openid profile email offline_access"
            };

            var response = await httpClient.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(requestBody),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Token refresh failed: {Error}", errorContent);
                return Result<AuthenticationResult>.Failure(ErrorType.Unauthorized, "Invalid refresh token");
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);

            if (tokenResponse is null)
            {
                logger.LogError("Failed to parse token response");
                return Result<AuthenticationResult>.Failure(ErrorType.Internal, "Token refresh failed");
            }

            var providerUserId = ExtractUserIdFromToken(tokenResponse.AccessToken);

            var result = new AuthenticationResult(
                AccessToken: tokenResponse.AccessToken,
                RefreshToken: tokenResponse.RefreshToken ?? refreshToken,
                ExpiresIn: tokenResponse.ExpiresIn,
                TokenType: tokenResponse.TokenType,
                ProviderUserId: providerUserId);

            return Result<AuthenticationResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh token");
            return Result<AuthenticationResult>.Failure(ErrorType.Internal, "Token refresh failed");
        }
    }

    public async Task<Result> SendVerificationEmailAsync(string providerUserId, CancellationToken ct = default)
    {
        try
        {
            // External ID handles email verification automatically
            // This is a no-op for now - verification emails are sent on user creation
            logger.LogInformation("Email verification requested for {ProviderUserId} (handled automatically)", providerUserId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send verification email for {ProviderUserId}", providerUserId);
            return Result.Failure(ErrorType.Internal, "Failed to send verification email");
        }
    }

    public async Task<Result> InitiatePasswordResetAsync(string email, CancellationToken ct = default)
    {
        try
        {
            // External ID handles password reset through self-service flows
            // This would require additional configuration in the Azure Portal
            logger.LogInformation("Password reset requested for {Email}", email);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initiate password reset for {Email}", email);
            return Result.Failure(ErrorType.Internal, "Failed to initiate password reset");
        }
    }

    public async Task<Result> UpdateUserTenantIdAsync(
        string providerUserId,
        Guid newTenantId,
        CancellationToken ct = default)
    {
        try
        {
            var graphClient = CreateGraphClient();

            var user = new User
            {
                AdditionalData = new Dictionary<string, object>
                {
                    ["extension_tenant_id"] = newTenantId.ToString()
                }
            };

            await graphClient.Users[providerUserId].PatchAsync(user, cancellationToken: ct);

            logger.LogInformation("Updated tenant ID for user {ProviderUserId} to {TenantId}", providerUserId, newTenantId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update tenant ID for user {ProviderUserId}", providerUserId);
            return Result.Failure(ErrorType.Internal, "Failed to update tenant ID");
        }
    }

    private static string ExtractUserIdFromToken(string accessToken)
    {
        try
        {
            // JWT is base64url encoded: header.payload.signature
            var parts = accessToken.Split('.');
            if (parts.Length != 3)
            {
                return "";
            }

            // Decode payload (second part)
            var payload = parts[1];
            var base64 = payload.Replace('-', '+').Replace('_', '/');
            var padLength = (4 - base64.Length % 4) % 4;
            base64 = base64.PadRight(base64.Length + padLength, '=');

            var decodedBytes = Convert.FromBase64String(base64);
            var decodedJson = System.Text.Encoding.UTF8.GetString(decodedBytes);

            using var doc = JsonDocument.Parse(decodedJson);
            if (doc.RootElement.TryGetProperty("oid", out var oidElement))
            {
                return oidElement.GetString() ?? "";
            }

            if (doc.RootElement.TryGetProperty("sub", out var subElement))
            {
                return subElement.GetString() ?? "";
            }

            return "";
        }
        catch
        {
            return "";
        }
    }

    private sealed record TokenResponse(
        string AccessToken,
        string? RefreshToken,
        int ExpiresIn,
        string TokenType);
}
