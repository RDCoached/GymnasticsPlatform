using Auth.Application.Services;
using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using Common.Core;
using FluentValidation;
using GymnasticsPlatform.Api.Extensions;
using GymnasticsPlatform.Api.Models;
using GymnasticsPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymnasticsPlatform.Api.Endpoints;

public sealed class AuthEndpoints : IEndpointGroup
{
    private static readonly Guid OnboardingTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/register", Register)
            .WithName("Register")
            .WithSummary("Register a new user with email and password")
            .AllowAnonymous();

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Authenticate with email and password")
            .AllowAnonymous();

        group.MapPost("/refresh", RefreshToken)
            .WithName("RefreshToken")
            .WithSummary("Refresh access token using refresh token")
            .AllowAnonymous();

        group.MapPost("/logout", Logout)
            .WithName("Logout")
            .WithSummary("Logout and clear session")
            .RequireAuthorization();

        group.MapPost("/forgot-password", ForgotPassword)
            .WithName("ForgotPassword")
            .WithSummary("Initiate password reset flow")
            .AllowAnonymous();

        group.MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .WithSummary("Get current authenticated user with roles")
            .Produces<CurrentUserResponse>()
            .RequireAuthorization();
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        IValidator<RegisterRequest> validator,
        IKeycloakAdminService keycloakService,
        AuthDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        // Validate request
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        // Check if email already exists
        var emailExistsResult = await keycloakService.EmailExistsAsync(request.Email, ct);
        if (!emailExistsResult.IsSuccess)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "Failed to check email availability");
        }

        if (emailExistsResult.Value)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: "User with this email already exists");
        }

        // Create user in Keycloak
        var createUserResult = await keycloakService.CreateUserAsync(
            request.Email,
            request.Password,
            request.FullName,
            OnboardingTenantId,
            ct);

        if (!createUserResult.IsSuccess)
        {
            return createUserResult.ErrorType switch
            {
                ErrorType.Conflict => Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    detail: createUserResult.ErrorMessage),
                ErrorType.Validation => Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: createUserResult.ErrorMessage),
                _ => Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    detail: "Failed to create user")
            };
        }

        var keycloakUserId = createUserResult.Value!;

        // Send verification email
        var sendEmailResult = await keycloakService.SendVerificationEmailAsync(keycloakUserId, ct);
        if (!sendEmailResult.IsSuccess)
        {
            // Log but don't fail registration - user can request resend later
            // (could also return a warning in the response)
        }

        // Create UserProfile in database
        var userProfile = UserProfile.Create(
            OnboardingTenantId,
            keycloakUserId,
            request.Email,
            request.FullName,
            clock.GetUtcNow());

        db.UserProfiles.Add(userProfile);
        await db.SaveChangesAsync(ct);

        var response = new RegisterResponse(
            Message: "Registration successful. Please check your email to verify your account.",
            RequiresEmailVerification: true);

        return Results.Created($"/api/auth/users/{keycloakUserId}", response);
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        IValidator<LoginRequest> validator,
        IKeycloakAdminService keycloakService,
        ISessionService sessionService,
        AuthDbContext db,
        HttpContext httpContext,
        TimeProvider clock,
        IHostEnvironment env,
        CancellationToken ct)
    {
        // Validate request
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        // Authenticate with Keycloak
        var authResult = await keycloakService.AuthenticateAsync(
            request.Email,
            request.Password,
            "user-portal",
            ct);

        if (!authResult.IsSuccess)
        {
            return authResult.ErrorType switch
            {
                ErrorType.Unauthorized => Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    detail: authResult.ErrorMessage),
                _ => Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    detail: "Authentication failed")
            };
        }

        var tokenResponse = authResult.Value!;

        // Decode JWT to get Keycloak user ID from the 'sub' claim
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenResponse.AccessToken);
        var keycloakUserId = jwtToken.Subject; // 'sub' claim contains Keycloak user ID

        // Get or create user profile (ignore tenant filter since login is anonymous)
        // Use case-insensitive email comparison
        var userProfile = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => EF.Functions.ILike(u.Email, request.Email), ct);

        if (userProfile is not null)
        {
            userProfile.RecordLogin(clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
        }

        // Create server-side session with Keycloak user ID
        var sessionId = await sessionService.CreateSessionAsync(
            keycloakUserId: keycloakUserId,
            accessToken: tokenResponse.AccessToken,
            refreshToken: tokenResponse.RefreshToken,
            expiry: TimeSpan.FromSeconds(tokenResponse.ExpiresIn),
            ct);

        // Set HTTP-only cookie (20 minute sliding expiration)
        // Use Lax in development (allows localhost cross-port), Strict in production
        httpContext.Response.Cookies.Append("session_id", sessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = env.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.Strict,
            MaxAge = TimeSpan.FromMinutes(20)
        });

        var userInfo = new UserInfo(
            Email: request.Email,
            FullName: userProfile?.FullName ?? "User",
            OnboardingCompleted: userProfile?.OnboardingCompleted ?? false);

        var response = new LoginResponse(
            AccessToken: tokenResponse.AccessToken,
            RefreshToken: tokenResponse.RefreshToken,
            ExpiresIn: tokenResponse.ExpiresIn,
            TokenType: tokenResponse.TokenType,
            User: userInfo);

        return Results.Ok(response);
    }

    private static async Task<IResult> Logout(
        HttpContext httpContext,
        ISessionService sessionService,
        CancellationToken ct)
    {
        if (httpContext.Request.Cookies.TryGetValue("session_id", out var sessionId))
        {
            await sessionService.DeleteSessionAsync(sessionId, ct);
            httpContext.Response.Cookies.Delete("session_id");
        }

        return Results.Ok(new { message = "Logged out successfully" });
    }

    private static async Task<IResult> RefreshToken(
        RefreshTokenRequest request,
        IValidator<RefreshTokenRequest> validator,
        IKeycloakAdminService keycloakService,
        CancellationToken ct)
    {
        // Validate request
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        // Refresh token with Keycloak
        var refreshResult = await keycloakService.RefreshTokenAsync(
            request.RefreshToken,
            "user-portal",
            ct);

        if (!refreshResult.IsSuccess)
        {
            return refreshResult.ErrorType switch
            {
                ErrorType.Unauthorized => Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    detail: refreshResult.ErrorMessage),
                _ => Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    detail: "Token refresh failed")
            };
        }

        var tokenResponse = refreshResult.Value!;

        var response = new RefreshTokenResponse(
            AccessToken: tokenResponse.AccessToken,
            RefreshToken: tokenResponse.RefreshToken,
            ExpiresIn: tokenResponse.ExpiresIn,
            TokenType: tokenResponse.TokenType);

        return Results.Ok(response);
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request,
        IValidator<ForgotPasswordRequest> validator,
        IKeycloakAdminService keycloakService,
        CancellationToken ct)
    {
        // Validate request
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        // Initiate password reset (always succeeds to prevent email enumeration)
        await keycloakService.InitiatePasswordResetAsync(request.Email, ct);

        var response = new ForgotPasswordResponse(
            Message: "If an account exists with this email, you will receive password reset instructions.");

        return Results.Ok(response);
    }

    private static async Task<IResult> GetCurrentUser(
        HttpContext httpContext,
        ITenantContext tenantContext,
        IRoleService roleService,
        AuthDbContext db,
        CancellationToken ct)
    {
        var userId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var tenantId = tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Tenant context not available");
        }

        // Get user profile
        var userProfile = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.ProviderUserId == userId && u.TenantId == tenantId.Value, ct);

        if (userProfile is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "User profile not found");
        }

        // Get user roles
        var roles = await roleService.GetUserRolesAsync(tenantId.Value, userId, ct);

        // Get club if user owns one
        var club = await db.Clubs
            .FirstOrDefaultAsync(c => c.OwnerUserId == userId, ct);

        var response = new CurrentUserResponse(
            UserId: userId,
            Email: userProfile.Email,
            Name: userProfile.FullName,
            TenantId: tenantId.Value,
            Roles: roles.Select(r => r.ToString()).ToList(),
            ClubId: club?.Id);

        return Results.Ok(response);
    }
}

public record CurrentUserResponse(
    string UserId,
    string Email,
    string Name,
    Guid TenantId,
    IReadOnlyList<string> Roles,
    Guid? ClubId);
