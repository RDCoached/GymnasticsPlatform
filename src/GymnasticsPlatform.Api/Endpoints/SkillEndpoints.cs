using Common.Core;
using GymnasticsPlatform.Api.Validators;
using Training.Application.Services;
using Training.Domain.Entities;
using Training.Domain.Enums;

namespace GymnasticsPlatform.Api.Endpoints;

public sealed class SkillEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/skills")
            .WithTags("Skills")
            .RequireAuthorization("CoachPolicy");

        group.MapGet("/", ListSkills)
            .WithName("ListSkills")
            .WithSummary("List skills with optional filters and pagination")
            .Produces<SkillListResponse>();

        group.MapGet("/{id:guid}", GetSkill)
            .WithName("GetSkill")
            .WithSummary("Get a skill by ID")
            .Produces<SkillResponse>()
            .ProducesProblem(404);

        group.MapPost("/search", SearchSkills)
            .WithName("SearchSkills")
            .WithSummary("Semantic search for skills")
            .Produces<List<SkillSearchResultResponse>>();

        group.MapPost("/", CreateSkill)
            .WithName("CreateSkill")
            .WithSummary("Create a new skill")
            .Produces<SkillResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", UpdateSkill)
            .WithName("UpdateSkill")
            .WithSummary("Update an existing skill")
            .Produces<SkillResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(404);

        group.MapDelete("/{id:guid}", DeleteSkill)
            .WithName("DeleteSkill")
            .WithSummary("Delete a skill")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(404)
            .ProducesProblem(409);
    }

    private static async Task<IResult> ListSkills(
        ISkillService skillService,
        GymnasticSection? section,
        int? minRating,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await skillService.ListAsync(section, minRating, pageNumber, pageSize, ct);

        if (!result.IsSuccess)
            return MapErrorToResult(result.ErrorType, result.ErrorMessage);

        var response = new SkillListResponse(
            Skills: result.Value!.Skills.Select(MapToResponse).ToList(),
            TotalCount: result.Value.TotalCount,
            PageNumber: result.Value.PageNumber,
            PageSize: result.Value.PageSize,
            TotalPages: result.Value.TotalPages);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetSkill(
        Guid id,
        ISkillService skillService,
        CancellationToken ct)
    {
        var result = await skillService.GetByIdAsync(id, ct);

        if (!result.IsSuccess)
            return MapErrorToResult(result.ErrorType, result.ErrorMessage);

        return Results.Ok(MapToResponse(result.Value!));
    }

    private static async Task<IResult> SearchSkills(
        SearchSkillsRequest request,
        ISkillService skillService,
        CancellationToken ct)
    {
        var result = await skillService.SearchAsync(
            request.Query,
            request.MaxResults ?? 10,
            request.Section,
            ct);

        if (!result.IsSuccess)
            return MapErrorToResult(result.ErrorType, result.ErrorMessage);

        var response = result.Value!
            .Select(r => new SkillSearchResultResponse(
                Skill: MapToResponse(r.Skill),
                SimilarityScore: r.SimilarityScore))
            .ToList();

        return Results.Ok(response);
    }

    private static async Task<IResult> CreateSkill(
        CreateSkillRequest request,
        ISkillService skillService,
        ITenantContext tenantContext,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("TenantId is required");

        var userId = httpContextAccessor.HttpContext?.Items["UserId"] as string
            ?? throw new InvalidOperationException("UserId is required");

        var result = await skillService.CreateAsync(
            request.Title,
            request.Description,
            request.EffectivenessRating,
            request.Sections,
            tenantId,
            Guid.Parse(userId),
            request.ImageUrl,
            ct);

        if (!result.IsSuccess)
            return MapErrorToResult(result.ErrorType, result.ErrorMessage);

        var response = MapToResponse(result.Value!);
        return Results.Created($"/api/skills/{response.Id}", response);
    }

    private static async Task<IResult> UpdateSkill(
        Guid id,
        UpdateSkillRequest request,
        ISkillService skillService,
        CancellationToken ct)
    {
        var result = await skillService.UpdateAsync(
            id,
            request.Title,
            request.Description,
            request.EffectivenessRating,
            request.Sections,
            request.ImageUrl,
            ct);

        if (!result.IsSuccess)
            return MapErrorToResult(result.ErrorType, result.ErrorMessage);

        return Results.Ok(MapToResponse(result.Value!));
    }

    private static async Task<IResult> DeleteSkill(
        Guid id,
        ISkillService skillService,
        CancellationToken ct)
    {
        var result = await skillService.DeleteAsync(id, ct);

        if (!result.IsSuccess)
            return MapErrorToResult(result.ErrorType, result.ErrorMessage);

        return Results.NoContent();
    }

    private static SkillResponse MapToResponse(Skill skill) => new(
        Id: skill.Id,
        Title: skill.Title,
        Description: skill.Description,
        EffectivenessRating: skill.EffectivenessRating,
        ImageUrl: skill.ImageUrl,
        UsageCount: skill.UsageCount,
        Sections: skill.Sections.Select(s => s.Section).ToList(),
        CreatedAt: skill.CreatedAt,
        LastModifiedAt: skill.LastModifiedAt);

    private static IResult MapErrorToResult(ErrorType? errorType, string? message) =>
        errorType switch
        {
            ErrorType.NotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: message),
            ErrorType.Validation => Results.ValidationProblem(
                new Dictionary<string, string[]> { ["error"] = [message ?? "Validation failed"] }),
            ErrorType.Conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: message),
            ErrorType.Unauthorized => Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: message),
            ErrorType.Forbidden => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: message),
            _ => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                detail: message ?? "An error occurred")
        };
}

// Response DTOs
public sealed record SkillResponse(
    Guid Id,
    string Title,
    string Description,
    int EffectivenessRating,
    string? ImageUrl,
    int UsageCount,
    IReadOnlyList<GymnasticSection> Sections,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModifiedAt);

public sealed record SkillListResponse(
    IReadOnlyList<SkillResponse> Skills,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

public sealed record SkillSearchResultResponse(
    SkillResponse Skill,
    double SimilarityScore);
