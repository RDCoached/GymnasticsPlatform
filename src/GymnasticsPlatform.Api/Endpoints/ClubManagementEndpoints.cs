using Auth.Application.Services;
using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using Common.Core;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace GymnasticsPlatform.Api.Endpoints;

public sealed class ClubManagementEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs/{clubId:guid}")
            .WithTags("Club Management")
            .RequireAuthorization("ClubAdminPolicy");

        group.MapPost("/invites", CreateInvite)
            .WithName("CreateClubInvite")
            .WithSummary("Create a new club invite")
            .ProducesValidationProblem();

        group.MapGet("/invites", ListInvites)
            .WithName("ListClubInvites")
            .WithSummary("List all club invites");

        group.MapPost("/members/{userId}/roles", AssignRole)
            .WithName("AssignMemberRole")
            .WithSummary("Assign a role to a club member")
            .ProducesValidationProblem();

        group.MapDelete("/members/{userId}/roles/{role}", RemoveRole)
            .WithName("RemoveMemberRole")
            .WithSummary("Remove a role from a club member");
    }

    private static async Task<IResult> CreateInvite(
        Guid clubId,
        CreateInviteRequest request,
        IValidator<CreateInviteRequest> validator,
        ITenantContext tenantContext,
        AuthDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var tenantId = tenantContext.TenantId;
        if (tenantId is null)
            return Results.Problem("Tenant context is required", statusCode: 400);

        // Verify club exists and belongs to current tenant
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null)
            return Results.NotFound(new { Message = "Club not found" });

        var expiresAt = clock.GetUtcNow().AddDays(request.ExpiryDays);
        var invite = ClubInvite.Create(
            clubId,
            request.InviteType,
            request.MaxUses,
            expiresAt,
            request.Description,
            clock);

        db.ClubInvites.Add(invite);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/clubs/{clubId}/invites/{invite.Id}",
            new InviteResponse(
                invite.Id,
                invite.Code,
                invite.InviteType,
                invite.MaxUses,
                invite.TimesUsed,
                invite.ExpiresAt,
                invite.CreatedAt,
                invite.Description));
    }

    private static async Task<IResult> ListInvites(
        Guid clubId,
        ITenantContext tenantContext,
        AuthDbContext db,
        CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId;
        if (tenantId is null)
            return Results.Problem("Tenant context is required", statusCode: 400);

        // Verify club exists and belongs to current tenant
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null)
            return Results.NotFound(new { Message = "Club not found" });

        var invites = await db.ClubInvites
            .Where(i => i.ClubId == clubId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InviteResponse(
                i.Id,
                i.Code,
                i.InviteType,
                i.MaxUses,
                i.TimesUsed,
                i.ExpiresAt,
                i.CreatedAt,
                i.Description))
            .ToListAsync(ct);

        return Results.Ok(invites);
    }

    private static async Task<IResult> AssignRole(
        Guid clubId,
        string userId,
        AssignRoleRequest request,
        IValidator<AssignRoleRequest> validator,
        ITenantContext tenantContext,
        AuthDbContext db,
        IRoleService roleService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var tenantId = tenantContext.TenantId;
        if (tenantId is null)
            return Results.Problem("Tenant context is required", statusCode: 400);

        // Verify club exists and belongs to current tenant
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null)
            return Results.NotFound(new { Message = "Club not found" });

        // Verify user exists in this tenant
        var userProfile = await db.UserProfiles
            .FirstOrDefaultAsync(u => u.KeycloakUserId == userId, ct);
        if (userProfile is null)
            return Results.NotFound(new { Message = "User not found in this tenant" });

        var assignedBy = httpContext.User.FindFirst("sub")?.Value;
        await roleService.AssignRolesAsync(
            tenantId.Value,
            userId,
            new List<Role> { request.Role }.AsReadOnly(),
            assignedBy,
            ct);

        return Results.NoContent();
    }

    private static async Task<IResult> RemoveRole(
        Guid clubId,
        string userId,
        Role role,
        ITenantContext tenantContext,
        AuthDbContext db,
        IRoleService roleService,
        CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId;
        if (tenantId is null)
            return Results.Problem("Tenant context is required", statusCode: 400);

        // Verify club exists and belongs to current tenant
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null)
            return Results.NotFound(new { Message = "Club not found" });

        // Prevent removing IndividualAdmin role in club context
        if (role == Role.IndividualAdmin)
            return Results.Problem("Cannot remove IndividualAdmin role in club context", statusCode: 400);

        await roleService.RemoveRoleAsync(tenantId.Value, userId, role, ct);

        return Results.NoContent();
    }
}

public record CreateInviteRequest(
    InviteType InviteType,
    int MaxUses,
    int ExpiryDays,
    string? Description);

public record AssignRoleRequest(Role Role);

public record InviteResponse(
    Guid Id,
    string Code,
    InviteType InviteType,
    int MaxUses,
    int TimesUsed,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    string? Description);
