using Auth.Application.Services;
using Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymnasticsPlatform.Api.Endpoints;

public sealed class AdminEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization("AdminPolicy");

        group.MapGet("/users", ListUsers)
            .WithName("ListUsers")
            .WithSummary("List all user profiles")
            .Produces<List<UserProfileResponse>>();

        group.MapPost("/users/{userId}/sync-tenant", SyncUserTenant)
            .WithName("SyncUserTenant")
            .WithSummary("Sync user's Keycloak tenant_id attribute with database value")
            .Produces<SyncTenantResponse>()
            .ProducesProblem(404)
            .ProducesProblem(500);
    }

    private static async Task<IResult> ListUsers(
        AuthDbContext db,
        CancellationToken ct)
    {
        // Get all user profiles (ignore tenant filter - this is admin operation)
        var users = await db.UserProfiles
            .IgnoreQueryFilters()
            .OrderBy(u => u.Email)
            .Select(u => new UserProfileResponse(
                u.Id,
                u.KeycloakUserId,
                u.Email,
                u.FullName,
                u.TenantId,
                u.OnboardingCompleted,
                u.OnboardingChoice))
            .ToListAsync(ct);

        return Results.Ok(users);
    }

    private static async Task<IResult> SyncUserTenant(
        string userId,
        AuthDbContext db,
        IKeycloakAdminService keycloakService,
        ILogger<AdminEndpoints> logger,
        CancellationToken ct)
    {
        // Find user in database (ignore tenant filter - this is admin operation)
        var userProfile = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.KeycloakUserId == userId, ct);

        if (userProfile is null)
        {
            logger.LogWarning("User {UserId} not found in database", userId);
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"User {userId} not found in database");
        }

        try
        {
            // Update Keycloak user's tenant_id attribute to match database
            await keycloakService.UpdateUserTenantIdAsync(userId, userProfile.TenantId, ct);

            logger.LogInformation(
                "Synced Keycloak tenant for user {UserId} ({Email}) to {TenantId}",
                userId, userProfile.Email, userProfile.TenantId);

            return Results.Ok(new SyncTenantResponse(
                UserId: userId,
                Email: userProfile.Email,
                TenantId: userProfile.TenantId,
                Message: "Keycloak tenant_id synced successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync Keycloak tenant for user {UserId}", userId);
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to sync tenant: {ex.Message}");
        }
    }
}

public record UserProfileResponse(
    Guid Id,
    string KeycloakUserId,
    string Email,
    string FullName,
    Guid TenantId,
    bool OnboardingCompleted,
    string? OnboardingChoice);

public record SyncTenantResponse(
    string UserId,
    string Email,
    Guid TenantId,
    string Message);
