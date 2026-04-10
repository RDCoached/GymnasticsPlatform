using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace GymnasticsPlatform.Api.Services;

public sealed class SessionService(
    IDistributedCache cache,
    TimeProvider timeProvider,
    ILogger<SessionService> logger) : ISessionService
{
    private const string SessionKeyPrefix = "session:";

    public async Task<string> CreateSessionAsync(string providerUserId, string accessToken, string refreshToken, TimeSpan expiry, CancellationToken ct = default)
    {
        var sessionId = GenerateSessionId();
        var sessionData = new SessionData
        {
            ProviderUserId = providerUserId,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = timeProvider.GetUtcNow().AddMinutes(20)
        };

        var json = JsonSerializer.Serialize(sessionData);
        var options = new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(20)
        };

        await cache.SetStringAsync($"{SessionKeyPrefix}{sessionId}", json, options, ct);

        logger.LogInformation("Session created for user {ProviderUserId} with session ID {SessionId}",
            providerUserId, sessionId);

        return sessionId;
    }

    public async Task<SessionData?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var json = await cache.GetStringAsync($"{SessionKeyPrefix}{sessionId}", ct);
        if (string.IsNullOrEmpty(json))
        {
            logger.LogWarning("Session {SessionId} not found in cache", sessionId);
            return null;
        }

        return JsonSerializer.Deserialize<SessionData>(json);
    }

    public async Task UpdateSessionAsync(string sessionId, SessionData data, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(data);
        var options = new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(20)
        };

        await cache.SetStringAsync($"{SessionKeyPrefix}{sessionId}", json, options, ct);

        logger.LogDebug("Session {SessionId} updated (expires at {ExpiresAt})",
            sessionId, data.ExpiresAt);
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await cache.RemoveAsync($"{SessionKeyPrefix}{sessionId}", ct);

        logger.LogInformation("Session {SessionId} deleted", sessionId);
    }

    private static string GenerateSessionId()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
