using Auth.Infrastructure.Persistence;
using GymnasticsPlatform.Api.Extensions;
using GymnasticsPlatform.Api.Middleware;
using GymnasticsPlatform.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Common.Core;
using Scalar.AspNetCore;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Add HTTP Context Accessor
builder.Services.AddHttpContextAccessor();

// Add Tenant Context
builder.Services.AddScoped<ITenantContext, TenantContext>();

// Add TimeProvider
builder.Services.AddSingleton(TimeProvider.System);

// Add User Tenant Service
builder.Services.AddScoped<Auth.Application.Services.IUserTenantService, Auth.Infrastructure.Services.UserTenantService>();

// Add Keycloak Admin Service
builder.Services.AddHttpClient<Auth.Application.Services.IKeycloakAdminService, Auth.Infrastructure.Services.KeycloakAdminService>();

// Add DbContext
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add OpenAPI
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "Gymnastics Platform API";
        document.Info.Version = "v1";
        document.Info.Description = "Multi-tenant gymnastics platform API with Keycloak authentication";
        return Task.CompletedTask;
    });
});

// Add Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var keycloakConfig = builder.Configuration.GetSection("Authentication:Keycloak");
        options.Authority = keycloakConfig["Authority"];
        options.Audience = keycloakConfig["Audience"];
        options.RequireHttpsMetadata = keycloakConfig.GetValue<bool>("RequireHttpsMetadata");
        options.MapInboundClaims = false; // Preserve original JWT claim names

        var validIssuers = keycloakConfig.GetSection("ValidIssuers").Get<string[]>();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuers = validIssuers
        };
    });

builder.Services.AddAuthorization();

// Add Exception Handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // Support ports 3001-3009 for local development flexibility
        var allowedOrigins = Enumerable.Range(3001, 9)
            .Select(port => $"http://localhost:{port}")
            .Concat(["http://localhost:5173", "http://localhost:5174"]) // Vite defaults
            .ToArray();

        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Add OpenTelemetry
var observabilityConfig = builder.Configuration.GetSection("Observability");
var serviceName = observabilityConfig["ServiceName"] ?? "gymnastics-api";
var serviceVersion = observabilityConfig["ServiceVersion"] ?? "1.0.0";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName, serviceVersion))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter()
            .AddOtlpExporter(options =>
            {
                var endpoint = observabilityConfig["OtlpEndpoint"] ?? "http://localhost:4318";
                options.Endpoint = new Uri(endpoint);
                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                options.ExportProcessorType = OpenTelemetry.ExportProcessorType.Simple;
            });
    })
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            var endpoint = observabilityConfig["OtlpEndpoint"] ?? "http://localhost:4318";
            options.Endpoint = new Uri(endpoint);
            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
        }));

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.AddOtlpExporter(options =>
    {
        var endpoint = observabilityConfig["OtlpEndpoint"] ?? "http://localhost:4318";
        options.Endpoint = new Uri(endpoint);
        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
    });
});

var app = builder.Build();

// Apply migrations in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await authDb.Database.MigrateAsync();
}

// Configure the HTTP request pipeline
app.UseExceptionHandler();

app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

// Auto-discover and register all endpoint groups
app.MapEndpoints();

// Health check endpoint (anonymous)
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }))
    .WithName("HealthCheck")
    .AllowAnonymous();

// Protected test endpoint
app.MapGet("/api/auth/me", (ITenantContext tenantContext, HttpContext httpContext) =>
{
    var user = httpContext.User;
    return Results.Ok(new
    {
        userId = user.FindFirst("sub")?.Value,
        email = user.FindFirst("email")?.Value,
        name = user.FindFirst("name")?.Value,
        tenantId = tenantContext.TenantId,
        roles = user.FindAll("roles").Select(c => c.Value).ToArray()
    });
})
.WithName("GetCurrentUser")
.RequireAuthorization();

app.Run();
