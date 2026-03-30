using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Auth.Application.Services;
using Common.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Auth.Infrastructure.Services;

public sealed class KeycloakAdminService : IKeycloakAdminService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KeycloakAdminService> _logger;
    private string? _accessToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    public KeycloakAdminService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<KeycloakAdminService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        var baseUrl = configuration["Keycloak:AdminBaseUrl"] ?? "http://localhost:8080";
        _httpClient.BaseAddress = new Uri(baseUrl);
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
            await EnsureAuthenticatedAsync(ct);

            var realm = _configuration["Keycloak:Realm"] ?? "gymnastics";
            var userUrl = $"/admin/realms/{realm}/users";

            var nameParts = fullName.Split(' ', 2);
            var firstName = nameParts[0];
            var lastName = nameParts.Length > 1 ? nameParts[1] : "";

            // In development, auto-verify emails to simplify testing
            var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
            var emailVerified = isDevelopment;
            var requiredActions = isDevelopment ? Array.Empty<string>() : new[] { "VERIFY_EMAIL" };

            var userPayload = new
            {
                username = email,
                email = email,
                emailVerified = emailVerified,
                enabled = true,
                firstName = firstName,
                lastName = lastName,
                attributes = new Dictionary<string, string[]>
                {
                    ["tenant_id"] = [tenantId.ToString()]
                },
                credentials = new[]
                {
                    new
                    {
                        type = "password",
                        value = password,
                        temporary = false
                    }
                },
                requiredActions = requiredActions
            };

            var request = new HttpRequestMessage(HttpMethod.Post, userUrl)
            {
                Content = JsonContent.Create(userPayload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                return Result.Failure<string>(ErrorType.Conflict, "User with this email already exists");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Bad request creating user: {Error}", errorContent);
                return Result.Failure<string>(ErrorType.Validation, "Invalid user data");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to create user in Keycloak. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);
                return Result.Failure<string>(ErrorType.Internal, "Failed to create user");
            }

            // Extract user ID from Location header
            var locationHeader = response.Headers.Location?.ToString();
            if (string.IsNullOrEmpty(locationHeader))
            {
                _logger.LogError("User created but no Location header returned");
                return Result.Failure<string>(ErrorType.Internal, "User created but ID not returned");
            }

            var userId = locationHeader.Split('/').Last();
            _logger.LogInformation("Successfully created user {Email} with ID {UserId}", email, userId);

            return Result.Success(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Email} in Keycloak", email);
            return Result.Failure<string>(ErrorType.Internal, "An error occurred while creating user");
        }
    }

    public async Task<Result<TokenResponse>> AuthenticateAsync(
        string email,
        string password,
        string clientId,
        CancellationToken ct = default)
    {
        try
        {
            var realm = _configuration["Keycloak:Realm"] ?? "gymnastics";
            var tokenUrl = $"/realms/{realm}/protocol/openid-connect/token";

            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = clientId,
                ["username"] = email,
                ["password"] = password
            };

            var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
            {
                Content = new FormUrlEncodedContent(formData)
            };

            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);

                if (errorContent.Contains("email not verified", StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Failure<TokenResponse>(ErrorType.Unauthorized,
                        "Email not verified. Please check your email for verification link.");
                }

                return Result.Failure<TokenResponse>(ErrorType.Unauthorized, "Invalid credentials");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to authenticate user. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);
                return Result.Failure<TokenResponse>(ErrorType.Internal, "Authentication failed");
            }

            var tokenData = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(ct);
            if (tokenData is null || string.IsNullOrEmpty(tokenData.AccessToken))
            {
                return Result.Failure<TokenResponse>(ErrorType.Internal, "Invalid token response");
            }

            var tokenResponse = new TokenResponse(
                tokenData.AccessToken,
                tokenData.RefreshToken ?? "",
                tokenData.ExpiresIn,
                tokenData.TokenType ?? "Bearer");

            return Result.Success(tokenResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating user {Email}", email);
            return Result.Failure<TokenResponse>(ErrorType.Internal, "An error occurred during authentication");
        }
    }

    public async Task<Result<TokenResponse>> RefreshTokenAsync(
        string refreshToken,
        string clientId,
        CancellationToken ct = default)
    {
        try
        {
            var realm = _configuration["Keycloak:Realm"] ?? "gymnastics";
            var tokenUrl = $"/realms/{realm}/protocol/openid-connect/token";

            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken
            };

            var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
            {
                Content = new FormUrlEncodedContent(formData)
            };

            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return Result.Failure<TokenResponse>(ErrorType.Unauthorized, "Invalid or expired refresh token");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to refresh token. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);
                return Result.Failure<TokenResponse>(ErrorType.Internal, "Token refresh failed");
            }

            var tokenData = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(ct);
            if (tokenData is null || string.IsNullOrEmpty(tokenData.AccessToken))
            {
                return Result.Failure<TokenResponse>(ErrorType.Internal, "Invalid token response");
            }

            var tokenResponse = new TokenResponse(
                tokenData.AccessToken,
                tokenData.RefreshToken ?? "",
                tokenData.ExpiresIn,
                tokenData.TokenType ?? "Bearer");

            return Result.Success(tokenResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return Result.Failure<TokenResponse>(ErrorType.Internal, "An error occurred while refreshing token");
        }
    }

    public async Task<Result> InitiatePasswordResetAsync(string email, CancellationToken ct = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(ct);

            // First, search for user by email
            var realm = _configuration["Keycloak:Realm"] ?? "gymnastics";
            var searchUrl = $"/admin/realms/{realm}/users?email={Uri.EscapeDataString(email)}&exact=true";

            var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            searchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var searchResponse = await _httpClient.SendAsync(searchRequest, ct);
            if (!searchResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to search for user {Email} for password reset", email);
                return Result.Success(); // Don't reveal if user exists
            }

            var users = await searchResponse.Content.ReadFromJsonAsync<JsonElement[]>(ct);
            if (users is null || users.Length == 0)
            {
                _logger.LogInformation("Password reset requested for non-existent email {Email}", email);
                return Result.Success(); // Don't reveal if user exists
            }

            var userId = users[0].GetProperty("id").GetString();
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Success();
            }

            // Send password reset email
            var resetUrl = $"/admin/realms/{realm}/users/{userId}/execute-actions-email";
            var resetPayload = new[] { "UPDATE_PASSWORD" };

            var resetRequest = new HttpRequestMessage(HttpMethod.Put, resetUrl)
            {
                Content = JsonContent.Create(resetPayload)
            };
            resetRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var resetResponse = await _httpClient.SendAsync(resetRequest, ct);
            if (!resetResponse.IsSuccessStatusCode)
            {
                var errorContent = await resetResponse.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Failed to send password reset email to {Email}. Status: {Status}, Error: {Error}",
                    email, resetResponse.StatusCode, errorContent);
            }

            return Result.Success(); // Always return success
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating password reset for {Email}", email);
            return Result.Success(); // Don't reveal errors
        }
    }

    public async Task<Result> SendVerificationEmailAsync(string keycloakUserId, CancellationToken ct = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(ct);

            var realm = _configuration["Keycloak:Realm"] ?? "gymnastics";
            var verifyUrl = $"/admin/realms/{realm}/users/{keycloakUserId}/execute-actions-email";
            var verifyPayload = new[] { "VERIFY_EMAIL" };

            var request = new HttpRequestMessage(HttpMethod.Put, verifyUrl)
            {
                Content = JsonContent.Create(verifyPayload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Result.Failure(ErrorType.NotFound, "User not found");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to send verification email. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);
                return Result.Failure(ErrorType.Internal, "Failed to send verification email");
            }

            _logger.LogInformation("Successfully sent verification email to user {UserId}", keycloakUserId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending verification email to user {UserId}", keycloakUserId);
            return Result.Failure(ErrorType.Internal, "An error occurred while sending verification email");
        }
    }

    public async Task<Result<bool>> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(ct);

            var realm = _configuration["Keycloak:Realm"] ?? "gymnastics";
            var searchUrl = $"/admin/realms/{realm}/users?email={Uri.EscapeDataString(email)}&exact=true";

            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to search for email. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);
                return Result.Failure<bool>(ErrorType.Internal, "Failed to check email existence");
            }

            var users = await response.Content.ReadFromJsonAsync<JsonElement[]>(ct);
            var exists = users is not null && users.Length > 0;

            return Result.Success(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if email {Email} exists", email);
            return Result.Failure<bool>(ErrorType.Internal, "An error occurred while checking email");
        }
    }

    public async Task UpdateUserTenantIdAsync(string keycloakUserId, Guid newTenantId, CancellationToken ct = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(ct);

            var realm = _configuration["Keycloak:Realm"] ?? "gymnastics";
            var userUrl = $"/admin/realms/{realm}/users/{keycloakUserId}";

            // GET the current user to preserve existing data
            var getRequest = new HttpRequestMessage(HttpMethod.Get, userUrl);
            getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var getResponse = await _httpClient.SendAsync(getRequest, ct);
            if (!getResponse.IsSuccessStatusCode)
            {
                var errorContent = await getResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Failed to get user {UserId} from Keycloak. Status: {Status}, Error: {Error}",
                    keycloakUserId, getResponse.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to get Keycloak user: {getResponse.StatusCode}");
            }

            var userJson = await getResponse.Content.ReadFromJsonAsync<JsonElement>(ct);

            // Create a mutable dictionary for attributes
            var attributes = new Dictionary<string, List<string>>();

            // Preserve existing attributes if any
            if (userJson.TryGetProperty("attributes", out var existingAttrs) && existingAttrs.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in existingAttrs.EnumerateObject())
                {
                    var values = new List<string>();
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in prop.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                                values.Add(item.GetString()!);
                        }
                    }
                    attributes[prop.Name] = values;
                }
            }

            // Update tenant_id
            attributes["tenant_id"] = [newTenantId.ToString()];

            // Build update payload with full user data
            var updatePayload = new
            {
                id = userJson.GetProperty("id").GetString(),
                username = userJson.GetProperty("username").GetString(),
                email = userJson.TryGetProperty("email", out var email) ? email.GetString() : null,
                emailVerified = userJson.TryGetProperty("emailVerified", out var emailVerified) && emailVerified.GetBoolean(),
                firstName = userJson.TryGetProperty("firstName", out var firstName) ? firstName.GetString() : null,
                lastName = userJson.TryGetProperty("lastName", out var lastName) ? lastName.GetString() : null,
                enabled = !userJson.TryGetProperty("enabled", out var enabled) || enabled.GetBoolean(),
                attributes = attributes
            };

            var putRequest = new HttpRequestMessage(HttpMethod.Put, userUrl)
            {
                Content = JsonContent.Create(updatePayload)
            };
            putRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var putResponse = await _httpClient.SendAsync(putRequest, ct);

            if (!putResponse.IsSuccessStatusCode)
            {
                var errorContent = await putResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Failed to update tenant_id for user {UserId} in Keycloak. Status: {Status}, Error: {Error}",
                    keycloakUserId, putResponse.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to update Keycloak user attributes: {putResponse.StatusCode}");
            }

            _logger.LogInformation(
                "Successfully updated tenant_id to {TenantId} for user {UserId}",
                newTenantId, keycloakUserId);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error updating Keycloak user {UserId} tenant_id", keycloakUserId);
            throw new InvalidOperationException("Failed to update Keycloak user attributes", ex);
        }
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _tokenExpiry)
            return;

        var clientId = _configuration["Keycloak:AdminClientId"] ?? "admin-cli";
        var username = _configuration["Keycloak:AdminUsername"] ?? throw new InvalidOperationException("Keycloak admin username not configured");
        var password = _configuration["Keycloak:AdminPassword"] ?? throw new InvalidOperationException("Keycloak admin password not configured");

        // Admin authentication must be against master realm, not the target realm
        var tokenUrl = "/realms/master/protocol/openid-connect/token";

        var formData = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["username"] = username,
            ["password"] = password
        };

        var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(formData)
        };

        var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to authenticate with Keycloak admin API. Status: {Status}, Error: {Error}",
                response.StatusCode, errorContent);
            throw new InvalidOperationException("Failed to authenticate with Keycloak admin API");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(ct);
        if (tokenResponse is null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            throw new InvalidOperationException("Received invalid token response from Keycloak");

        _accessToken = tokenResponse.AccessToken;
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 30); // 30s buffer

        _logger.LogDebug("Successfully authenticated with Keycloak admin API");
    }

    private sealed record KeycloakTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string? TokenType);
}
