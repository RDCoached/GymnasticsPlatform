using System.Security.Claims;
using System.Text.Json;
using GymnasticsPlatform.Api.Configuration;
using GymnasticsPlatform.Api.Services;
using Microsoft.Extensions.Options;

namespace GymnasticsPlatform.Api.Middleware;

public sealed class SessionAuthMiddleware(
    RequestDelegate next,
    IOptions<KeycloakSettings> keycloakSettings,
    ILogger<SessionAuthMiddleware> logger)
{
    private readonly KeycloakSettings _keycloakSettings = keycloakSettings.Value;

    public async Task InvokeAsync(
        HttpContext context,
        ISessionService sessionService)
    {
        if (!context.Request.Cookies.TryGetValue("session_id", out var sessionId))
        {
            await next(context);
            return;
        }

        var sessionData = await sessionService.GetSessionAsync(sessionId, context.RequestAborted);
        if (sessionData == null)
        {
            logger.LogWarning("Session {SessionId} not found in cache", sessionId);
            context.Response.Cookies.Delete("session_id");
            await next(context);
            return;
        }

        if (sessionData.ExpiresAt <= TimeProvider.System.GetUtcNow())
        {
            try
            {
                var refreshedToken = await RefreshTokenAsync(
                    sessionData.RefreshToken,
                    _keycloakSettings,
                    context.RequestAborted);

                sessionData = sessionData with
                {
                    AccessToken = refreshedToken.AccessToken,
                    RefreshToken = refreshedToken.RefreshToken,
                    ExpiresAt = TimeProvider.System.GetUtcNow().AddSeconds(refreshedToken.ExpiresIn)
                };

                await sessionService.UpdateSessionAsync(sessionId, sessionData, context.RequestAborted);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to refresh token for session {SessionId}", sessionId);
                context.Response.Cookies.Delete("session_id");
                await sessionService.DeleteSessionAsync(sessionId, context.RequestAborted);
                await next(context);
                return;
            }
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sessionData.KeycloakUserId),
            new("sub", sessionData.KeycloakUserId)
        };

        var identity = new ClaimsIdentity(claims, "Session");
        var principal = new ClaimsPrincipal(identity);

        context.User = principal;
        context.Items["AccessToken"] = sessionData.AccessToken;

        await next(context);
    }

    private static async Task<TokenResponse> RefreshTokenAsync(
        string refreshToken,
        KeycloakSettings settings,
        CancellationToken ct)
    {
        using var httpClient = new HttpClient();
        var tokenEndpoint = $"{settings.Authority}/protocol/openid-connect/token";

        var requestBody = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = settings.ClientId,
            ["client_secret"] = settings.ClientSecret
        };

        var response = await httpClient.PostAsync(
            tokenEndpoint,
            new FormUrlEncodedContent(requestBody),
            ct);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json);

        if (tokenResponse == null)
            throw new InvalidOperationException("Failed to deserialize token response");

        return tokenResponse;
    }

    private sealed record TokenResponse
    {
        public required string access_token { get; init; }
        public required string refresh_token { get; init; }
        public required int expires_in { get; init; }

        public string AccessToken => access_token;
        public string RefreshToken => refresh_token;
        public int ExpiresIn => expires_in;
    }
}
