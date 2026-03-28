using Auth.Application.Services;
using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using Common.Core;
using Microsoft.EntityFrameworkCore;

namespace GymnasticsPlatform.Api.Endpoints;

public sealed class OnboardingEndpoints : IEndpointGroup
{
    private static readonly Guid OnboardingTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/onboarding")
            .WithTags("Onboarding");

        group.MapGet("/status", GetOnboardingStatus)
            .WithName("GetOnboardingStatus")
            .WithSummary("Get user's onboarding status")
            .RequireAuthorization();

        group.MapPost("/create-club", CreateClub)
            .WithName("CreateClub")
            .WithSummary("Create a new club and complete onboarding")
            .RequireAuthorization();

        group.MapPost("/join-club", JoinClub)
            .WithName("JoinClub")
            .WithSummary("Join an existing club via invite code")
            .RequireAuthorization();

        group.MapPost("/individual", ChooseIndividualMode)
            .WithName("ChooseIndividualMode")
            .WithSummary("Choose individual mode and complete onboarding")
            .RequireAuthorization();
    }

    private static async Task<IResult> GetOnboardingStatus(
        ITenantContext tenantContext,
        AuthDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var tenantId = tenantContext.TenantId ?? Guid.Empty;
        var isOnboardingTenant = tenantId == OnboardingTenantId;

        // Check if user profile exists and get onboarding status
        var userProfile = await db.UserProfiles
            .FirstOrDefaultAsync(u => u.KeycloakUserId == userId, ct);

        return Results.Ok(new OnboardingStatusResponse(
            Completed: userProfile?.OnboardingCompleted ?? false,
            IsOnboardingTenant: isOnboardingTenant,
            TenantId: tenantId,
            OnboardingChoice: userProfile?.OnboardingChoice
        ));
    }

    private static async Task<IResult> CreateClub(
        CreateClubRequest request,
        ITenantContext tenantContext,
        AuthDbContext db,
        HttpContext httpContext,
        TimeProvider clock,
        IUserTenantService userTenantService,
        CancellationToken ct)
    {
        var userId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var tenantId = tenantContext.TenantId ?? Guid.Empty;
        if (tenantId != OnboardingTenantId)
            return Results.Problem("User is not in onboarding tenant", statusCode: 400);

        // Create club with new tenant ID
        var club = Club.Create(request.Name, userId, clock);
        db.Clubs.Add(club);

        // Update user profile onboarding status
        var userProfile = await db.UserProfiles
            .FirstOrDefaultAsync(u => u.KeycloakUserId == userId, ct);

        if (userProfile is not null)
        {
            userProfile.CompleteOnboarding("club");
        }

        await db.SaveChangesAsync(ct);

        // Update user's tenant in database
        var email = httpContext.User.FindFirst("email")?.Value;
        var fullName = httpContext.User.FindFirst("name")?.Value;
        await userTenantService.UpdateUserTenantAsync(userId, club.TenantId, email, fullName, ct);

        return Results.Ok(new OnboardingCompleteResponse(
            TenantId: club.TenantId,
            Role: "organization_owner",
            ClubId: club.Id
        ));
    }

    private static async Task<IResult> JoinClub(
        JoinClubRequest request,
        ITenantContext tenantContext,
        AuthDbContext db,
        HttpContext httpContext,
        TimeProvider clock,
        IUserTenantService userTenantService,
        CancellationToken ct)
    {
        var userId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var tenantId = tenantContext.TenantId ?? Guid.Empty;
        if (tenantId != OnboardingTenantId)
            return Results.Problem("User is not in onboarding tenant", statusCode: 400);

        // Find invite by code (ignore query filters - user is in onboarding tenant)
        var invite = await db.ClubInvites
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Code == request.InviteCode, ct);

        if (invite is null)
            return Results.Problem("Invalid invite code", statusCode: 404);

        if (invite.IsExpired(clock.GetUtcNow()))
            return Results.Problem("Invite has expired", statusCode: 400);

        if (invite.IsAtMaxUses())
            return Results.Problem("Invite has reached maximum uses", statusCode: 400);

        // Mark invite as used
        invite.MarkAsUsed(clock);

        // Get club to retrieve tenant ID (ignore query filters - user is in onboarding tenant)
        var club = await db.Clubs.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == invite.ClubId, ct);
        if (club is null)
            return Results.Problem("Club not found", statusCode: 404);

        // Update user profile onboarding status
        var userProfile = await db.UserProfiles
            .FirstOrDefaultAsync(u => u.KeycloakUserId == userId, ct);

        if (userProfile is not null)
        {
            userProfile.CompleteOnboarding("club");
        }

        await db.SaveChangesAsync(ct);

        // Update user's tenant in database
        var email = httpContext.User.FindFirst("email")?.Value;
        var fullName = httpContext.User.FindFirst("name")?.Value;
        await userTenantService.UpdateUserTenantAsync(userId, club.TenantId, email, fullName, ct);

        return Results.Ok(new OnboardingCompleteResponse(
            TenantId: club.TenantId,
            Role: "member",
            ClubId: club.Id
        ));
    }

    private static async Task<IResult> ChooseIndividualMode(
        ITenantContext tenantContext,
        AuthDbContext db,
        HttpContext httpContext,
        IUserTenantService userTenantService,
        CancellationToken ct)
    {
        var userId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var tenantId = tenantContext.TenantId ?? Guid.Empty;
        if (tenantId != OnboardingTenantId)
            return Results.Problem("User is not in onboarding tenant", statusCode: 400);

        // Generate unique tenant ID for individual user
        var newTenantId = Guid.NewGuid();

        // Update user profile onboarding status
        var userProfile = await db.UserProfiles
            .FirstOrDefaultAsync(u => u.KeycloakUserId == userId, ct);

        if (userProfile is not null)
        {
            userProfile.CompleteOnboarding("individual");
        }

        await db.SaveChangesAsync(ct);

        // Update user's tenant in database
        var email = httpContext.User.FindFirst("email")?.Value;
        var fullName = httpContext.User.FindFirst("name")?.Value;
        await userTenantService.UpdateUserTenantAsync(userId, newTenantId, email, fullName, ct);

        return Results.Ok(new OnboardingCompleteResponse(
            TenantId: newTenantId,
            Role: "individual",
            ClubId: null
        ));
    }
}

public record OnboardingStatusResponse(
    bool Completed,
    bool IsOnboardingTenant,
    Guid TenantId,
    string? OnboardingChoice);

public record CreateClubRequest(string Name);

public record JoinClubRequest(string InviteCode);

public record OnboardingCompleteResponse(
    Guid TenantId,
    string Role,
    Guid? ClubId);
