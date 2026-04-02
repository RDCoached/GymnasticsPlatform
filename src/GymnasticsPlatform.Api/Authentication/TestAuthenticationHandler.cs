using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace GymnasticsPlatform.Api;

/// <summary>
/// Test authentication handler that allows all requests without validation.
/// ONLY used in E2E test mode - never in production.
/// </summary>
public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Use X-Test-User-Id header if present, otherwise use a default
        // This allows tests to use different user IDs to avoid conflicts
        var testUserId = Context.Request.Headers["X-Test-User-Id"].FirstOrDefault()
                         ?? $"e2e-test-user-{Guid.NewGuid()}";

        var email = Context.Request.Headers["X-Test-User-Email"].FirstOrDefault()
                    ?? $"{testUserId}@test.com";

        var name = Context.Request.Headers["X-Test-User-Name"].FirstOrDefault()
                   ?? "E2E Test User";

        // Create a test user with necessary claims for E2E tests
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, testUserId),
            new Claim("sub", testUserId),
            new Claim("email", email),
            new Claim("name", name),
            new Claim(ClaimTypes.Role, "platform_admin") // Give admin role for all tests
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
