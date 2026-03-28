using GymnasticsPlatform.Api.Extensions;

namespace GymnasticsPlatform.Api.Endpoints;

/// <summary>
/// Test endpoints for exception handling verification.
/// Only available in Development environment.
/// </summary>
public sealed class TestEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var environment = app.ServiceProvider.GetRequiredService<IHostEnvironment>();

        // Only register test endpoints in development or test environments
        if (!environment.IsDevelopment() && environment.EnvironmentName != "Test")
        {
            return;
        }

        var group = app.MapGroup("/api/test").WithTags("Test");

        group.MapGet("/throw", () =>
        {
            throw new Exception("Test exception");
        })
        .WithName("ThrowException")
        .AllowAnonymous();

        group.MapGet("/throw-argument", () =>
        {
            throw new ArgumentException("Invalid argument");
        })
        .WithName("ThrowArgumentException")
        .AllowAnonymous();

        group.MapGet("/throw-unauthorized", () =>
        {
            throw new UnauthorizedAccessException("Unauthorized");
        })
        .WithName("ThrowUnauthorizedException")
        .AllowAnonymous();

        group.MapGet("/throw-invalid-operation", () =>
        {
            throw new InvalidOperationException("Invalid operation");
        })
        .WithName("ThrowInvalidOperationException")
        .AllowAnonymous();
    }
}
