using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationScheme = "TestScheme";
    public const string TenantIdClaimType = "tenant_id";
    public const string UserIdClaimType = "sub";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-User-Id"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers["X-Test-User-Id"].ToString();
        var tenantId = Request.Headers["X-Test-Tenant-Id"].ToString();
        var email = Request.Headers["X-Test-Email"].ToString();
        var username = Request.Headers["X-Test-Username"].ToString();
        var roles = Request.Headers["X-Test-Roles"].ToString();

        var claims = new List<Claim>
        {
            new(UserIdClaimType, userId),
            new("email", email),
            new("name", username)
        };

        if (!string.IsNullOrEmpty(tenantId))
        {
            claims.Add(new Claim(TenantIdClaimType, tenantId));
        }

        if (!string.IsNullOrEmpty(roles))
        {
            foreach (var role in roles.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
            }
        }

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers["WWW-Authenticate"] = $"Bearer realm=\"{AuthenticationScheme}\"";
        Response.StatusCode = 401;
        return Task.CompletedTask;
    }
}
