using GymnasticsPlatform.Api.Services;

namespace GymnasticsPlatform.Integration.Tests.Mocks;

public sealed class MockSessionService : ISessionService
{
    private readonly Dictionary<string, SessionData> _sessions = new();

    public Task<string> CreateSessionAsync(string providerUserId, string accessToken, string refreshToken, TimeSpan expiry, CancellationToken ct = default)
    {
        var sessionId = Guid.NewGuid().ToString();
        var sessionData = new SessionData
        {
            ProviderUserId = providerUserId,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.Add(expiry)
        };

        _sessions[sessionId] = sessionData;
        return Task.FromResult(sessionId);
    }

    public Task<SessionData?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        _sessions.TryGetValue(sessionId, out var sessionData);
        return Task.FromResult(sessionData);
    }

    public Task UpdateSessionAsync(string sessionId, SessionData data, CancellationToken ct = default)
    {
        _sessions[sessionId] = data;
        return Task.CompletedTask;
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        _sessions.Remove(sessionId);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _sessions.Clear();
    }
}
