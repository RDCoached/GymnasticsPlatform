using Auth.Application.Services;
using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymnasticsPlatform.Api.Endpoints;

public sealed class AdminEndpoints : IEndpointGroup
{
    private static readonly Guid OnboardingTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization("AdminPolicy");

        group.MapGet("/users", ListUsers)
            .WithName("ListUsers")
            .WithSummary("List all users with pagination and search");

        group.MapGet("/users/{userId}", GetUser)
            .WithName("GetUser")
            .WithSummary("Get detailed user information including roles");

        group.MapPost("/users/{userId}/roles", AssignRoles)
            .WithName("AssignRoles")
            .WithSummary("Assign roles to a user");

        group.MapDelete("/users/{userId}/roles", RemoveRoles)
            .WithName("RemoveRoles")
            .WithSummary("Remove roles from a user");

        group.MapPost("/users/{userId}/reset-onboarding", ResetOnboarding)
            .WithName("ResetOnboarding")
            .WithSummary("Reset user to onboarding state");

        group.MapPost("/users/{userId}/complete-onboarding", CompleteOnboarding)
            .WithName("CompleteOnboarding")
            .WithSummary("Manually complete user onboarding");

        group.MapGet("/users/{userId}/audit-log", GetUserAuditLog)
            .WithName("GetUserAuditLog")
            .WithSummary("Get audit log for a user");
    }

    private static async Task<IResult> ListUsers(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? email,
        AuthDbContext db,
        CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

        var query = db.UserProfiles
            .IgnoreQueryFilters()
            .AsQueryable();

        if (!string.IsNullOrEmpty(email))
            query = query.Where(u => EF.Functions.ILike(u.Email, $"%{email}%"));

        var totalCount = await query.CountAsync(ct);

        var users = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FullName,
                u.TenantId,
                u.OnboardingCompleted,
                u.OnboardingChoice,
                u.CreatedAt,
                u.LastLoginAt
            })
            .ToListAsync(ct);

        return Results.Ok(new
        {
            users,
            pagination = new
            {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }
        });
    }

    private static async Task<IResult> GetUser(
        Guid userId,
        AuthDbContext db,
        IRoleService roleService,
        CancellationToken ct)
    {
        var user = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return Results.NotFound();

        var roles = await roleService.GetUserRolesAsync(user.TenantId, user.KeycloakUserId, ct);

        var club = await db.Clubs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OwnerUserId == user.KeycloakUserId, ct);

        return Results.Ok(new
        {
            user.Id,
            user.KeycloakUserId,
            user.Email,
            user.FullName,
            user.TenantId,
            user.OnboardingCompleted,
            user.OnboardingChoice,
            user.CreatedAt,
            user.LastLoginAt,
            roles = roles.Select(r => r.ToString()).ToList(),
            clubId = club?.Id,
            clubName = club?.Name
        });
    }

    private static async Task<IResult> AssignRoles(
        Guid userId,
        [FromBody] AssignRolesRequest request,
        HttpContext httpContext,
        AuthDbContext db,
        IRoleService roleService,
        IAuditService auditService,
        CancellationToken ct)
    {
        var user = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return Results.NotFound();

        var adminUserId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(adminUserId) || !Guid.TryParse(adminUserId, out var adminUserGuid))
            return Results.Unauthorized();

        var roles = request.RoleNames
            .Select(name => Enum.TryParse<Role>(name, out var role) ? role : (Role?)null)
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();

        await roleService.AssignRolesAsync(user.TenantId, user.KeycloakUserId, roles, adminUserId, ct);
        await db.SaveChangesAsync(ct);

        await auditService.LogActionAsync(
            performedByUserId: adminUserGuid,
            action: "AssignRoles",
            entityType: "UserProfile",
            entityId: userId.ToString(),
            oldValue: null,
            newValue: string.Join(", ", request.RoleNames),
            ct: ct);

        return Results.Ok();
    }

    private static async Task<IResult> RemoveRoles(
        Guid userId,
        [FromBody] RemoveRolesRequest request,
        HttpContext httpContext,
        AuthDbContext db,
        IRoleService roleService,
        IAuditService auditService,
        CancellationToken ct)
    {
        var user = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return Results.NotFound();

        var adminUserId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(adminUserId) || !Guid.TryParse(adminUserId, out var adminUserGuid))
            return Results.Unauthorized();

        foreach (var roleName in request.RoleNames)
        {
            if (Enum.TryParse<Role>(roleName, out var role))
            {
                await roleService.RemoveRoleAsync(user.TenantId, user.KeycloakUserId, role, ct);
            }
        }
        await db.SaveChangesAsync(ct);

        await auditService.LogActionAsync(
            performedByUserId: adminUserGuid,
            action: "RemoveRoles",
            entityType: "UserProfile",
            entityId: userId.ToString(),
            oldValue: string.Join(", ", request.RoleNames),
            newValue: null,
            ct: ct);

        return Results.Ok();
    }

    private static async Task<IResult> ResetOnboarding(
        Guid userId,
        HttpContext httpContext,
        IUserTenantService userTenantService,
        IRoleService roleService,
        IAuditService auditService,
        AuthDbContext db,
        CancellationToken ct)
    {
        var user = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return Results.NotFound();

        var adminUserId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(adminUserId) || !Guid.TryParse(adminUserId, out var adminUserGuid))
            return Results.Unauthorized();

        var oldTenantId = user.TenantId;
        var oldOnboardingStatus = user.OnboardingCompleted;

        await userTenantService.UpdateUserTenantAsync(user.KeycloakUserId, OnboardingTenantId, user.Email, user.FullName, ct);

        var existingRoles = await roleService.GetUserRolesAsync(user.TenantId, user.KeycloakUserId, ct);
        foreach (var role in existingRoles)
        {
            await roleService.RemoveRoleAsync(user.TenantId, user.KeycloakUserId, role, ct);
        }

        user.ResetOnboarding();
        await db.SaveChangesAsync(ct);

        await auditService.LogActionAsync(
            performedByUserId: adminUserGuid,
            action: "ResetOnboarding",
            entityType: "UserProfile",
            entityId: userId.ToString(),
            oldValue: $"TenantId: {oldTenantId}, OnboardingCompleted: {oldOnboardingStatus}",
            newValue: $"TenantId: {OnboardingTenantId}, OnboardingCompleted: false",
            ct: ct);

        return Results.Ok();
    }

    private static async Task<IResult> CompleteOnboarding(
        Guid userId,
        [FromBody] CompleteOnboardingRequest request,
        HttpContext httpContext,
        IAuditService auditService,
        AuthDbContext db,
        CancellationToken ct)
    {
        var user = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return Results.NotFound();

        var adminUserId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(adminUserId) || !Guid.TryParse(adminUserId, out var adminUserGuid))
            return Results.Unauthorized();

        var oldOnboardingStatus = user.OnboardingCompleted;
        var oldChoice = user.OnboardingChoice;

        user.CompleteOnboarding(request.Choice);
        await db.SaveChangesAsync(ct);

        await auditService.LogActionAsync(
            performedByUserId: adminUserGuid,
            action: "CompleteOnboarding",
            entityType: "UserProfile",
            entityId: userId.ToString(),
            oldValue: $"OnboardingCompleted: {oldOnboardingStatus}, Choice: {oldChoice}",
            newValue: $"OnboardingCompleted: true, Choice: {request.Choice}",
            ct: ct);

        return Results.Ok();
    }

    private static async Task<IResult> GetUserAuditLog(
        Guid userId,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        AuthDbContext db,
        CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

        var user = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return Results.NotFound();

        var userIdString = userId.ToString();

        var query = db.AuditLogs
            .Where(a => a.EntityType == "UserProfile" && a.EntityId == userIdString)
            .OrderByDescending(a => a.PerformedAt);

        var totalCount = await query.CountAsync(ct);

        var logs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.PerformedByUserId,
                a.Action,
                a.OldValue,
                a.NewValue,
                a.PerformedAt,
                a.IpAddress,
                a.UserAgent
            })
            .ToListAsync(ct);

        return Results.Ok(new
        {
            logs,
            pagination = new
            {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }
        });
    }
}

public sealed record AssignRolesRequest(string[] RoleNames);
public sealed record RemoveRolesRequest(string[] RoleNames);
public sealed record CompleteOnboardingRequest(string Choice);
