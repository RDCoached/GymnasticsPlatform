namespace GymnasticsPlatform.Api.Endpoints;

public sealed class OnboardingEndpoints : IEndpointGroup
{
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

    private static IResult GetOnboardingStatus()
    {
        // TODO: Implement onboarding status check
        return Results.Ok(new { completed = false });
    }

    private static IResult CreateClub()
    {
        // TODO: Implement club creation
        return Results.Created("/api/clubs/123", new { tenantId = Guid.NewGuid() });
    }

    private static IResult JoinClub()
    {
        // TODO: Implement join club logic
        return Results.Ok(new { tenantId = Guid.NewGuid() });
    }

    private static IResult ChooseIndividualMode()
    {
        // TODO: Implement individual mode logic
        return Results.Ok(new { tenantId = Guid.NewGuid() });
    }
}
