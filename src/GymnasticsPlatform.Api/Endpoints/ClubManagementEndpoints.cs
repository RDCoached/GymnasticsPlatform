using Auth.Application.Services;
using Auth.Domain.Entities;
using Auth.Infrastructure.Configuration;
using Auth.Infrastructure.Persistence;
using Common.Core;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymnasticsPlatform.Api.Endpoints;

public sealed class ClubManagementEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs/{clubId:guid}")
            .WithTags("Club Management")
            .RequireAuthorization("ClubAdminPolicy");

        group.MapPost("/invites/send-email", SendEmailInvite)
            .WithName("SendEmailInvite")
            .WithSummary("Send email invitation to join club")
            .ProducesValidationProblem()
            .Produces<EmailInviteResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status429TooManyRequests);

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
                i.Description,
                i.Email,
                i.SentAt))
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

    private static async Task<IResult> SendEmailInvite(
        Guid clubId,
        SendEmailInviteRequest request,
        IValidator<SendEmailInviteRequest> validator,
        ITenantContext tenantContext,
        AuthDbContext db,
        IEmailService emailService,
        IOptions<EmailSettings> emailSettings,
        TimeProvider clock,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var tenantId = tenantContext.TenantId;
        if (tenantId is null)
            return Results.Problem("Tenant context required", statusCode: 400);

        // Verify club exists in current tenant
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null)
            return Results.NotFound(new { Message = "Club not found" });

        // Rate limit: max 10 email invites per hour per club
        var oneHourAgo = clock.GetUtcNow().AddHours(-1);
        var recentInvites = await db.ClubInvites
            .Where(i => i.ClubId == clubId && i.Email != null && i.CreatedAt > oneHourAgo)
            .CountAsync(ct);

        if (recentInvites >= 10)
            return Results.Problem("Rate limit exceeded. Maximum 10 invites per hour.", statusCode: 429);

        // Prevent duplicate active invites to same email
        var existingInvite = await db.ClubInvites
            .Where(i => i.ClubId == clubId
                && i.Email == request.Email
                && i.ExpiresAt > clock.GetUtcNow()
                && i.TimesUsed < i.MaxUses)
            .FirstOrDefaultAsync(ct);

        if (existingInvite is not null)
            return Results.Conflict(new { Message = "Active invitation already exists for this email" });

        // Create single-use email invite
        var expiresAt = clock.GetUtcNow().AddDays(7);
        var invite = ClubInvite.Create(
            clubId,
            request.InviteType,
            maxUses: 1,
            expiresAt,
            request.Description,
            request.Email,
            clock);

        // Construct invite URL with auto-fill code
        var baseUrl = emailSettings.Value.BaseUrl;
        var inviteUrl = $"{baseUrl}/register?inviteCode={invite.Code}";

        // Use transaction to ensure invite is only saved if email sends successfully
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.ClubInvites.Add(invite);
            await db.SaveChangesAsync(ct);

            // Send email via Resend - if this fails, transaction will rollback
            await emailService.SendClubInviteAsync(
                request.Email,
                club.Name,
                invite.Code,
                inviteUrl,
                request.InviteType,
                ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        return Results.Created(
            $"/api/clubs/{clubId}/invites/{invite.Id}",
            new EmailInviteResponse(
                invite.Id,
                invite.Code,
                invite.Email!,
                invite.InviteType,
                invite.ExpiresAt,
                invite.SentAt!.Value,
                invite.Description));
    }
}

public record AssignRoleRequest(Role Role);

public record SendEmailInviteRequest(
    string Email,
    InviteType InviteType,
    string? Description);

public record EmailInviteResponse(
    Guid Id,
    string Code,
    string Email,
    InviteType InviteType,
    DateTimeOffset ExpiresAt,
    DateTimeOffset SentAt,
    string? Description);

public record InviteResponse(
    Guid Id,
    string Code,
    InviteType InviteType,
    int MaxUses,
    int TimesUsed,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    string? Description,
    string? Email,
    DateTimeOffset? SentAt);
