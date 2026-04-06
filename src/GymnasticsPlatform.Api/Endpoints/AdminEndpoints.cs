using Auth.Application.Services;
using Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Training.Infrastructure.Seeders;

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

        group.MapPost("/seed-skills", SeedSkills)
            .WithName("SeedSkills")
            .WithSummary("Seed the skills catalog with common gymnastics skills")
            .Produces<SeedSkillsResponse>();
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

    private static async Task<IResult> SeedSkills(
        SkillSeeder seeder,
        ILogger<AdminEndpoints> logger,
        CancellationToken ct)
    {
        try
        {
            // Use a system tenant/user ID for seeded skills
            var systemTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

            logger.LogInformation("Starting skills catalog seeding via admin endpoint");
            await seeder.SeedAsync(systemTenantId, systemUserId, ct);

            return Results.Ok(new SeedSkillsResponse(
                Message: "Skills catalog seeded successfully",
                SystemTenantId: systemTenantId,
                SystemUserId: systemUserId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed skills catalog");
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to seed skills: {ex.Message}");
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

public record SeedSkillsResponse(
    string Message,
    Guid SystemTenantId,
    Guid SystemUserId);
