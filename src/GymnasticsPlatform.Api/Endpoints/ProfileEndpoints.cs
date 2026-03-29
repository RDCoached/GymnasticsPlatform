using Auth.Infrastructure.Persistence;
using Common.Core;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace GymnasticsPlatform.Api.Endpoints;

public sealed class ProfileEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile")
            .WithTags("Profile")
            .RequireAuthorization();

        group.MapGet("/", GetProfile)
            .WithName("GetProfile")
            .WithSummary("Get current user's profile")
            .Produces<ProfileResponse>();

        group.MapPut("/", UpdateProfile)
            .WithName("UpdateProfile")
            .WithSummary("Update current user's profile")
            .Produces<ProfileResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(404);
    }

    private static async Task<IResult> GetProfile(
        HttpContext httpContext,
        AuthDbContext db,
        CancellationToken ct)
    {
        var userId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var userProfile = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.KeycloakUserId == userId, ct);

        if (userProfile is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "User profile not found");
        }

        return Results.Ok(new ProfileResponse(
            userProfile.Email,
            userProfile.FullName,
            userProfile.OnboardingCompleted));
    }

    private static async Task<IResult> UpdateProfile(
        UpdateProfileRequest request,
        IValidator<UpdateProfileRequest> validator,
        HttpContext httpContext,
        AuthDbContext db,
        CancellationToken ct)
    {
        // Validate request
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var userId = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var userProfile = await db.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.KeycloakUserId == userId, ct);

        if (userProfile is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "User profile not found");
        }

        // Update profile
        userProfile.UpdateProfile(request.FullName);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new ProfileResponse(
            userProfile.Email,
            userProfile.FullName,
            userProfile.OnboardingCompleted));
    }
}

public record UpdateProfileRequest(string FullName);

public record ProfileResponse(
    string Email,
    string FullName,
    bool OnboardingCompleted);

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
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
