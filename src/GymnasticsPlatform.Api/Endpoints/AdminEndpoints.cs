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
            .WithSummary("Sync user's authentication provider tenant_id attribute with database value")
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
                u.ProviderUserId,
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
        IAuthenticationProvider authProvider,
        ILogger<AdminEndpoints> logger,
        CancellationToken ct)
    {
        // Find user in database (ignore tenant filter - this is admin operation)
        var userProfile = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.ProviderUserId == userId, ct);

        if (userProfile is null)
        {
            logger.LogWarning("User {UserId} not found in database", userId);
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"User {userId} not found in database");
        }

        // Update authentication provider's tenant_id attribute to match database
        var result = await authProvider.UpdateUserTenantIdAsync(userId, userProfile.TenantId, ct);

        if (!result.IsSuccess)
        {
            logger.LogError("Failed to sync provider tenant for user {UserId}: {Error}", userId, result.ErrorMessage);
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                detail: $"Failed to sync tenant: {result.ErrorMessage}");
        }

        logger.LogInformation(
            "Synced provider tenant for user {UserId} ({Email}) to {TenantId}",
            userId, userProfile.Email, userProfile.TenantId);

        return Results.Ok(new SyncTenantResponse(
            UserId: userId,
            Email: userProfile.Email,
            TenantId: userProfile.TenantId,
            Message: "Provider tenant_id synced successfully"));
    }
}

public record UserProfileResponse(
    Guid Id,
    string ProviderUserId,
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
