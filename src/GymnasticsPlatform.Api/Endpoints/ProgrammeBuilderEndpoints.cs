using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Training.Application.Services;

namespace GymnasticsPlatform.Api.Endpoints;

public sealed class ProgrammeBuilderEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/programme-builder")
            .WithTags("Programme Builder");

        group.MapPost("/start", StartSessionAsync)
            .WithName("StartProgrammeBuilderSession")
            .WithSummary("Start a new RAG-powered programme builder session")
            .Produces<BuilderSessionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/continue/{sessionId:guid}", ContinueSessionAsync)
            .WithName("ContinueProgrammeBuilderSession")
            .WithSummary("Continue an existing programme builder session")
            .Produces<BuilderSessionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/accept/{sessionId:guid}", AcceptSessionAsync)
            .WithName("AcceptProgrammeBuilderSession")
            .WithSummary("Accept the current suggestion and create the programme")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Results<Ok<BuilderSessionResult>, BadRequest<string>>> StartSessionAsync(
        [FromBody] StartSessionRequest request,
        IProgrammeBuilderService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.StartSessionAsync(
                request.GymnastId,
                request.Goals,
                request.RagScope ?? "gymnast",
                cancellationToken);

            return TypedResults.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok<BuilderSessionResult>, BadRequest<string>, NotFound<string>>> ContinueSessionAsync(
        Guid sessionId,
        [FromBody] ContinueSessionRequest request,
        IProgrammeBuilderService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ContinueSessionAsync(
                sessionId,
                request.Message,
                cancellationToken);

            return TypedResults.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return TypedResults.NotFound(ex.Message);
        }
    }

    private static async Task<Results<Ok<Guid>, NotFound<string>>> AcceptSessionAsync(
        Guid sessionId,
        IProgrammeBuilderService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var programmeId = await service.AcceptSessionAsync(sessionId, cancellationToken);
            return TypedResults.Ok(programmeId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return TypedResults.NotFound(ex.Message);
        }
    }
}

public sealed record StartSessionRequest(
    Guid GymnastId,
    string Goals,
    string? RagScope = "gymnast");

public sealed record ContinueSessionRequest(string Message);
