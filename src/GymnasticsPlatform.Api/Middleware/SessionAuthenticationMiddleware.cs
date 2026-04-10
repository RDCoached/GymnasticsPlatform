using GymnasticsPlatform.Api.Services;
using System.Security.Claims;

namespace GymnasticsPlatform.Api.Middleware;

/// <summary>
/// Middleware that authenticates requests using session cookies.
/// Reads the session_id cookie, validates it, and sets httpContext.User.
/// </summary>
public sealed class SessionAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public SessionAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISessionService sessionService)
    {
        // Check if session cookie exists
        if (context.Request.Cookies.TryGetValue("session_id", out var sessionId))
        {
            try
            {
                // Retrieve session from store
                var session = await sessionService.GetSessionAsync(sessionId, context.RequestAborted);

                if (session is not null)
                {
                    // Create claims from session
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.NameIdentifier, session.ProviderUserId),
                        new("sub", session.ProviderUserId),
                    };

                    var identity = new ClaimsIdentity(claims, "Session");
                    var principal = new ClaimsPrincipal(identity);

                    // Set the user principal for this request
                    context.User = principal;
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail the request - let it proceed as unauthenticated
                Console.WriteLine($"Session authentication failed: {ex.Message}");
            }
        }

        await _next(context);
    }
}
