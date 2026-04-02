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
        // Create a test user with necessary claims for E2E tests
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "e2e-test-user"),
            new Claim("sub", "e2e-test-user"),
            new Claim("email", "e2e@test.com"),
            new Claim("name", "E2E Test User"),
            new Claim(ClaimTypes.Role, "platform_admin") // Give admin role for all tests
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
