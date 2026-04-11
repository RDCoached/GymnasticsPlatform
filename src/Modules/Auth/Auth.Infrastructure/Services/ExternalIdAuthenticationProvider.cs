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
    private readonly string _ciamDomain = configuration["Authentication:ExternalId:CiamDomain"]
        ?? throw new InvalidOperationException("ExternalId CiamDomain not configured");
    private readonly string _apiClientId = configuration["Authentication:ExternalId:ApiClientId"]
        ?? throw new InvalidOperationException("ExternalId ApiClientId not configured");
    private readonly string _apiClientSecret = configuration["Authentication:ExternalId:ApiClientSecret"]
        ?? throw new InvalidOperationException("ExternalId ApiClientSecret not configured");
    private readonly string _authority = configuration["Authentication:ExternalId:Authority"]
        ?? $"https://login.microsoftonline.com/{configuration["Authentication:ExternalId:TenantId"]}";

    // Log configuration on startup for debugging
    private readonly bool _startupLogged = LogStartupConfiguration(logger, configuration["Authentication:ExternalId:Authority"]
        ?? $"https://login.microsoftonline.com/{configuration["Authentication:ExternalId:TenantId"]}");

    private static bool LogStartupConfiguration(ILogger logger, string authority)
    {
        logger.LogInformation("🔐 ExternalIdAuthenticationProvider initialized");
        logger.LogInformation("   Authority: {Authority}", authority);
        logger.LogInformation("   Token Endpoint: {TokenEndpoint}", $"{authority}/oauth2/v2.0/token");
        return true;
    }

    private GraphServiceClient CreateGraphClient()
    {
        var options = new ClientSecretCredentialOptions
        {
            AuthorityHost = new Uri(_authority)
        };

        var credential = new ClientSecretCredential(
            _tenantId,
            _apiClientId,
            _apiClientSecret,
            options);

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
            logger.LogInformation("Creating user in External ID: {Email}, TenantId: {TenantId}, ConfigTenantId: {ConfigTenantId}",
                email, tenantId, _tenantId);

            var graphClient = CreateGraphClient();

            // External ID requires UserPrincipalName to use a verified domain
            // Use the CIAM tenant domain for UPN, actual email goes in Mail property
            var username = email.Split('@')[0];
            var userPrincipalName = $"{username}@{_ciamDomain}";

            var user = new User
            {
                AccountEnabled = true,
                DisplayName = fullName,
                MailNickname = username,
                UserPrincipalName = userPrincipalName,
                Mail = email,
                PasswordProfile = new PasswordProfile
                {
                    ForceChangePasswordNextSignIn = false,
                    Password = password
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
                    config.QueryParameters.Select = ["id", "mail", "displayName", "userPrincipalName", "otherMails", "identities"];
                }, cancellationToken: ct);

            if (user is null)
            {
                return Result<ProviderUserInfo?>.Success(null);
            }

            // For federated users (Google, etc.), try multiple sources for actual email
            var email = user.Mail; // Primary email (often null for federated users)

            // Try otherMails (alternative email addresses)
            if (string.IsNullOrWhiteSpace(email) && user.OtherMails?.Any() == true)
            {
                email = user.OtherMails.First();
            }

            // Try identities (federated identities like Google)
            if (string.IsNullOrWhiteSpace(email) && user.Identities?.Any() == true)
            {
                var googleIdentity = user.Identities.FirstOrDefault(i => i.Issuer == "google.com");
                if (googleIdentity?.IssuerAssignedId != null)
                {
                    email = googleIdentity.IssuerAssignedId; // Google email
                }
            }

            // Final fallback to UPN
            if (string.IsNullOrWhiteSpace(email))
            {
                email = user.UserPrincipalName ?? "";
            }

            var userInfo = new ProviderUserInfo(
                ProviderUserId: user.Id ?? providerUserId,
                Email: email,
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

            // External ID requires UserPrincipalName (username@ciamDomain) for ROPC flow
            var username = email.Split('@')[0];
            var userPrincipalName = $"{username}@{_ciamDomain}";

            var requestBody = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = _apiClientId,
                ["client_secret"] = _apiClientSecret,
                ["scope"] = $"api://{_tenantId}/gymnastics-api/user.access openid profile email offline_access",
                ["username"] = userPrincipalName,
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

            logger.LogInformation("✅ Successfully obtained token for {Email}, expires in {ExpiresIn}s", email, tokenResponse.ExpiresIn);

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
        string? codeVerifier = null,
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
                ["scope"] = $"api://{_tenantId}/gymnastics-api/user.access openid profile email offline_access"
            };

            // Add PKCE code verifier if provided (required for SPAs)
            if (!string.IsNullOrEmpty(codeVerifier))
            {
                requestBody["code_verifier"] = codeVerifier;
            }

            var response = await httpClient.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(requestBody),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("❌ Token exchange failed with status {StatusCode}", response.StatusCode);
                logger.LogError("   Token Endpoint: {TokenEndpoint}", tokenEndpoint);
                logger.LogError("   Error Response: {Error}", errorContent);
                return Result<AuthenticationResult>.Failure(ErrorType.Unauthorized, $"Token exchange failed: {errorContent}");
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
                ["scope"] = $"api://{_tenantId}/gymnastics-api/user.access openid profile email offline_access"
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
        // Tenant ID is stored in our database (UserProfile table), not in External ID
        // This is a no-op for External ID provider
        logger.LogInformation("Tenant ID update requested for user {ProviderUserId} to {TenantId} (stored in database only)",
            providerUserId, newTenantId);
        return await Task.FromResult(Result.Success());
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
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn,
        [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType);
}
