using Microsoft.AspNetCore.Http.HttpResults;
using Training.Application.Services;

namespace GymnasticsPlatform.Api.Endpoints;

public sealed class ProgrammeEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/programmes")
            .WithTags("Programmes")
            .RequireAuthorization("CoachPolicy");

        group.MapGet("/{id:guid}", GetProgrammeAsync)
            .WithName("GetProgramme")
            .WithSummary("Get a programme by ID")
            .Produces<ProgrammeDetailsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/gymnast/{gymnastId:guid}", ListProgrammesByGymnastAsync)
            .WithName("ListProgrammesByGymnast")
            .WithSummary("List all programmes for a gymnast")
            .Produces<List<ProgrammeSummary>>(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}/activate", ActivateProgrammeAsync)
            .WithName("ActivateProgramme")
            .WithSummary("Activate a programme (deactivates others)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/complete", CompleteProgrammeAsync)
            .WithName("CompleteProgramme")
            .WithSummary("Mark a programme as completed")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/archive", ArchiveProgrammeAsync)
            .WithName("ArchiveProgramme")
            .WithSummary("Archive a completed programme")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteProgrammeAsync)
            .WithName("DeleteProgramme")
            .WithSummary("Delete a programme")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<ProgrammeDetailsResponse>, NotFound>> GetProgrammeAsync(
        Guid id,
        IProgrammeService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);

        if (result is null)
            return TypedResults.NotFound();

        var (metadata, document) = result.Value;

        return TypedResults.Ok(new ProgrammeDetailsResponse
        {
            Id = metadata.Id,
            Title = metadata.Title,
            Status = metadata.Status.ToString(),
            GymnastId = metadata.GymnastId,
            CoachId = metadata.CoachId,
            StartDate = metadata.StartDate,
            EndDate = metadata.EndDate,
            Goals = document.Goals,
            Content = document.Content
        });
    }

    private static async Task<Ok<List<ProgrammeSummary>>> ListProgrammesByGymnastAsync(
        Guid gymnastId,
        IProgrammeService service,
        CancellationToken cancellationToken)
    {
        var programmes = await service.ListByGymnastAsync(gymnastId, cancellationToken);

        var summaries = programmes.Select(p => new ProgrammeSummary
        {
            Id = p.Id,
            Title = p.Title,
            Status = p.Status.ToString(),
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            CreatedAt = p.CreatedAt
        }).ToList();

        return TypedResults.Ok(summaries);
    }

    private static async Task<Results<NoContent, NotFound>> ActivateProgrammeAsync(
        Guid id,
        IProgrammeService service,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.ActivateAsync(id, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
    }

    private static async Task<Results<NoContent, NotFound>> CompleteProgrammeAsync(
        Guid id,
        IProgrammeService service,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.CompleteAsync(id, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
    }

    private static async Task<Results<NoContent, NotFound>> ArchiveProgrammeAsync(
        Guid id,
        IProgrammeService service,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.ArchiveAsync(id, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
    }

    private static async Task<Results<NoContent, NotFound>> DeleteProgrammeAsync(
        Guid id,
        IProgrammeService service,
        CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(id, cancellationToken);

        if (!deleted)
            return TypedResults.NotFound();

        return TypedResults.NoContent();
    }
}

public sealed record ProgrammeDetailsResponse
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid GymnastId { get; init; }
    public Guid CoachId { get; init; }
    public DateTimeOffset StartDate { get; init; }
    public DateTimeOffset EndDate { get; init; }
    public string Goals { get; init; } = string.Empty;
    public object? Content { get; init; }
}

public sealed record ProgrammeSummary
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset StartDate { get; init; }
    public DateTimeOffset EndDate { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
