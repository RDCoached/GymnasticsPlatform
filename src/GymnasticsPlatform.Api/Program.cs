using Auth.Infrastructure.Persistence;
using GymnasticsPlatform.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Common.Core;

var builder = WebApplication.CreateBuilder(args);

// Add HTTP Context Accessor
builder.Services.AddHttpContextAccessor();

// Add Tenant Context
builder.Services.AddScoped<ITenantContext, TenantContext>();

// Add DbContext
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add OpenAPI
builder.Services.AddOpenApi();

// Add Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var keycloakConfig = builder.Configuration.GetSection("Authentication:Keycloak");
        options.Authority = keycloakConfig["Authority"];
        options.Audience = keycloakConfig["Audience"];
        options.RequireHttpsMetadata = keycloakConfig.GetValue<bool>("RequireHttpsMetadata");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    });

builder.Services.AddAuthorization();

// Add OpenTelemetry
var observabilityConfig = builder.Configuration.GetSection("Observability");
var serviceName = observabilityConfig["ServiceName"] ?? "gymnastics-api";
var serviceVersion = observabilityConfig["ServiceVersion"] ?? "1.0.0";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName, serviceVersion))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(observabilityConfig["OtlpEndpoint"] ?? "http://localhost:4318");
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(observabilityConfig["OtlpEndpoint"] ?? "http://localhost:4318");
        }));

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(observabilityConfig["OtlpEndpoint"] ?? "http://localhost:4318");
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
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

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
        tenantId = tenantContext.TenantId,
        roles = user.FindAll("roles").Select(c => c.Value).ToArray()
    });
})
.WithName("GetCurrentUser")
.RequireAuthorization();

app.Run();
