namespace GymnasticsPlatform.Api.Services;

public interface ISessionService
{
    Task<string> CreateSessionAsync(string keycloakUserId, string accessToken, string refreshToken, TimeSpan expiry, CancellationToken ct = default);
    Task<SessionData?> GetSessionAsync(string sessionId, CancellationToken ct = default);
    Task UpdateSessionAsync(string sessionId, SessionData data, CancellationToken ct = default);
    Task DeleteSessionAsync(string sessionId, CancellationToken ct = default);
}

public sealed record SessionData
{
    public required string KeycloakUserId { get; init; }
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
