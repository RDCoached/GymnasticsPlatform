using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GymnasticsPlatform.Api.Middleware;

/// <summary>
/// Global exception handler that converts all unhandled exceptions to RFC 9457 ProblemDetails.
/// Prevents stack trace leakage in production.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = MapExceptionToStatusCode(exception);

        // Log the full exception securely (not returned to client)
        logger.LogError(
            exception,
            "Unhandled exception occurred. Status: {StatusCode}, Path: {Path}, Method: {Method}",
            statusCode,
            httpContext.Request.Path,
            httpContext.Request.Method);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = $"https://tools.ietf.org/html/rfc9457#section-{GetRfcSection(statusCode)}",
            Instance = httpContext.Request.Path
        };

        // Add traceId for correlation
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true; // Exception handled
    }

    private static (int StatusCode, string Title) MapExceptionToStatusCode(Exception exception)
    {
        return exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "An error occurred while processing your request.")
        };
    }

    private static string GetRfcSection(int statusCode)
    {
        return statusCode switch
        {
            400 => "8.2",
            401 => "8.3",
            409 => "8.10",
            500 => "8.6",
            _ => "8.6"
        };
    }
}
