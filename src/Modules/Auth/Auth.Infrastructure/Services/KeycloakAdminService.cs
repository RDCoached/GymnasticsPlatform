using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Auth.Application.Services;
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

    public async Task UpdateUserTenantIdAsync(string keycloakUserId, Guid newTenantId, CancellationToken ct = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(ct);

            var realm = _configuration["Keycloak:Realm"] ?? "gymnastics";
            var url = $"/admin/realms/{realm}/users/{keycloakUserId}";

            var updatePayload = new
            {
                attributes = new Dictionary<string, List<string>>
                {
                    ["tenant_id"] = [newTenantId.ToString()]
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = JsonContent.Create(updatePayload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Failed to update tenant_id for user {UserId} in Keycloak. Status: {Status}, Error: {Error}",
                    keycloakUserId, response.StatusCode, errorContent);
                throw new InvalidOperationException($"Failed to update Keycloak user attributes: {response.StatusCode}");
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

        var realm = _configuration["Keycloak:Realm"] ?? "gymnastics";
        var clientId = _configuration["Keycloak:AdminClientId"] ?? "admin-cli";
        var username = _configuration["Keycloak:AdminUsername"] ?? throw new InvalidOperationException("Keycloak admin username not configured");
        var password = _configuration["Keycloak:AdminPassword"] ?? throw new InvalidOperationException("Keycloak admin password not configured");

        var tokenUrl = $"/realms/{realm}/protocol/openid-connect/token";

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
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
