using Auth.Application.Services;
using Auth.Domain.Entities;
using Auth.Infrastructure.Persistence;
using Training.Infrastructure.Persistence;
using Common.Core;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GymnasticsPlatform.Api.Endpoints;

public sealed class GymnastEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/gymnasts")
            .WithTags("Gymnasts")
            .RequireAuthorization("CoachPolicy");

        group.MapGet("/", ListGymnastsAsync)
            .WithName("ListGymnasts")
            .WithSummary("List all gymnasts in current tenant")
            .Produces<List<GymnastResponse>>(StatusCodes.Status200OK);

        group.MapGet("/coach/{coachId:guid}", ListGymnastsByCoachAsync)
            .WithName("ListGymnastsByCoach")
            .WithSummary("List all gymnasts assigned to a specific coach (based on programmes)")
            .Produces<List<GymnastResponse>>(StatusCodes.Status200OK);

        group.MapPost("/", CreateGymnastAsync)
            .WithName("CreateGymnast")
            .WithSummary("Create a new gymnast")
            .Produces<GymnastResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", UpdateGymnastAsync)
            .WithName("UpdateGymnast")
            .WithSummary("Update a gymnast")
            .Produces<GymnastResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteGymnastAsync)
            .WithName("DeleteGymnast")
            .WithSummary("Remove gymnast role from user")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<Ok<List<GymnastResponse>>> ListGymnastsAsync(
        AuthDbContext db,
        ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("TenantId is required");

        // Get all users with Gymnast role in current tenant
        var gymnasts = await (
            from profile in db.UserProfiles
            join role in db.UserRoles on profile.KeycloakUserId equals role.KeycloakUserId
            where role.Role == Role.Gymnast && profile.TenantId == tenantId
            select new GymnastResponse(
                profile.Id,
                profile.FullName,
                profile.Email
            )
        ).ToListAsync(cancellationToken);

        return TypedResults.Ok(gymnasts);
    }

    private static async Task<Ok<List<GymnastResponse>>> ListGymnastsByCoachAsync(
        Guid coachId,
        AuthDbContext authDb,
        TrainingDbContext trainingDb,
        ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("TenantId is required");

        // Get unique gymnast IDs from programmes created by this coach
        var gymnastIds = await trainingDb.ProgrammeMetadata
            .Where(p => p.CoachId == coachId && p.TenantId == tenantId)
            .Select(p => p.GymnastId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (gymnastIds.Count == 0)
        {
            return TypedResults.Ok(new List<GymnastResponse>());
        }

        // Get profile details for these gymnasts
        var gymnasts = await (
            from profile in authDb.UserProfiles
            join role in authDb.UserRoles on profile.KeycloakUserId equals role.KeycloakUserId
            where role.Role == Role.Gymnast
                && profile.TenantId == tenantId
                && gymnastIds.Contains(profile.Id)
            select new GymnastResponse(
                profile.Id,
                profile.FullName,
                profile.Email
            )
        ).ToListAsync(cancellationToken);

        return TypedResults.Ok(gymnasts);
    }

    private static async Task<Results<Created<GymnastResponse>, ValidationProblem>> CreateGymnastAsync(
        CreateGymnastRequest request,
        IValidator<CreateGymnastRequest> validator,
        AuthDbContext db,
        IRoleService roleService,
        ITenantContext tenantContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Validate request
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("TenantId is required");

        // Check if email already exists in this tenant
        var existingUser = await db.UserProfiles
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.TenantId == tenantId, cancellationToken);

        if (existingUser is not null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Email"] = ["A user with this email already exists in your organization."]
            });
        }

        // Create user profile
        var keycloakUserId = $"gymnast-{Guid.NewGuid()}"; // In production, this would be created via Keycloak
        var userProfile = UserProfile.Create(
            tenantId,
            keycloakUserId,
            request.Email,
            request.FullName,
            DateTimeOffset.UtcNow);

        db.UserProfiles.Add(userProfile);
        await db.SaveChangesAsync(cancellationToken);

        // Assign Gymnast role
        var coachUserId = httpContext.User.FindFirst("sub")?.Value ?? "system";
        await roleService.AssignRolesAsync(
            tenantId,
            keycloakUserId,
            [Role.Gymnast],
            coachUserId,
            cancellationToken);

        var response = new GymnastResponse(userProfile.Id, userProfile.FullName, userProfile.Email);

        return TypedResults.Created($"/api/gymnasts/{userProfile.Id}", response);
    }

    private static async Task<Results<Ok<GymnastResponse>, ValidationProblem, NotFound>> UpdateGymnastAsync(
        Guid id,
        UpdateGymnastRequest request,
        IValidator<UpdateGymnastRequest> validator,
        AuthDbContext db,
        ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        // Validate request
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("TenantId is required");

        // Get gymnast profile
        var userProfile = await db.UserProfiles
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId, cancellationToken);

        if (userProfile is null)
        {
            return TypedResults.NotFound();
        }

        // Verify user has Gymnast role
        var hasGymnastRole = await db.UserRoles
            .AnyAsync(ur => ur.KeycloakUserId == userProfile.KeycloakUserId && ur.Role == Role.Gymnast, cancellationToken);

        if (!hasGymnastRole)
        {
            return TypedResults.NotFound();
        }

        // Update profile
        userProfile.UpdateProfile(request.FullName);
        await db.SaveChangesAsync(cancellationToken);

        var response = new GymnastResponse(userProfile.Id, userProfile.FullName, userProfile.Email);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteGymnastAsync(
        Guid id,
        AuthDbContext db,
        ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("TenantId is required");

        // Get gymnast profile
        var userProfile = await db.UserProfiles
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId, cancellationToken);

        if (userProfile is null)
        {
            return TypedResults.NotFound();
        }

        // Remove Gymnast role
        var gymnastRole = await db.UserRoles
            .FirstOrDefaultAsync(ur => ur.KeycloakUserId == userProfile.KeycloakUserId && ur.Role == Role.Gymnast, cancellationToken);

        if (gymnastRole is null)
        {
            return TypedResults.NotFound();
        }

        db.UserRoles.Remove(gymnastRole);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}

public sealed record CreateGymnastRequest(string Email, string FullName);

public sealed record UpdateGymnastRequest(string FullName);

public sealed record GymnastResponse(Guid Id, string Name, string Email);

public sealed class CreateGymnastRequestValidator : AbstractValidator<CreateGymnastRequest>
{
    public CreateGymnastRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Email must be valid");

        RuleFor(x => x.FullName)
            .NotEmpty()
            .WithMessage("Full name is required")
            .MinimumLength(2)
            .WithMessage("Full name must be at least 2 characters")
            .MaximumLength(100)
            .WithMessage("Full name must not exceed 100 characters");
    }
}

public sealed class UpdateGymnastRequestValidator : AbstractValidator<UpdateGymnastRequest>
{
    public UpdateGymnastRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .WithMessage("Full name is required")
            .MinimumLength(2)
            .WithMessage("Full name must be at least 2 characters")
            .MaximumLength(100)
            .WithMessage("Full name must not exceed 100 characters");
    }
}
