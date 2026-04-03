using Auth.Infrastructure.Persistence;
using GymnasticsPlatform.Api;
using GymnasticsPlatform.Api.Authorization;
using GymnasticsPlatform.Api.Extensions;
using GymnasticsPlatform.Api.Middleware;
using GymnasticsPlatform.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Resend;
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

// Add Role Service
builder.Services.AddScoped<Auth.Application.Services.IRoleService, Auth.Infrastructure.Services.RoleService>();

// Email Service with Resend
builder.Services.AddOptions<ResendClientOptions>()
    .Configure(options => options.ApiToken = builder.Configuration["Resend:ApiKey"] ?? string.Empty);
builder.Services.AddHttpClient<ResendClient>();
builder.Services.AddTransient<IResend, ResendClient>();

builder.Services.Configure<Auth.Infrastructure.Configuration.EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<Auth.Application.Services.IEmailService, Auth.Infrastructure.Services.ResendEmailService>();

// Add Keycloak Admin Service
builder.Services.AddHttpClient<Auth.Application.Services.IKeycloakAdminService, Auth.Infrastructure.Services.KeycloakAdminService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});

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
        // Use internal Keycloak address for fetching signing keys
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

// Register Authorization Handler
builder.Services.AddSingleton<IAuthorizationHandler, GymnasticsPlatform.Api.Authorization.TenantRoleAuthorizationHandler>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy =>
        policy.RequireRole("platform_admin"));

    options.AddPolicy("ClubAdminPolicy", policy =>
        policy.RequireAuthenticatedUser()
              .RequireTenantRole(Auth.Domain.Entities.Role.ClubAdmin));

    options.AddPolicy("CoachPolicy", policy =>
        policy.RequireAuthenticatedUser()
              .RequireTenantRole(
                  Auth.Domain.Entities.Role.Coach,
                  Auth.Domain.Entities.Role.ClubAdmin,
                  Auth.Domain.Entities.Role.IndividualAdmin));

    options.AddPolicy("GymnastPolicy", policy =>
        policy.RequireAuthenticatedUser()
              .RequireTenantRole(
                  Auth.Domain.Entities.Role.Gymnast,
                  Auth.Domain.Entities.Role.Coach,
                  Auth.Domain.Entities.Role.ClubAdmin,
                  Auth.Domain.Entities.Role.IndividualAdmin));
});

// Add Exception Handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AuthDbContext>("database");

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // In development, allow all origins for easier testing
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        }
        else
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:3001", "http://localhost:3002", "http://localhost:5173"];

            policy.WithOrigins(allowedOrigins)
                .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
                .WithHeaders("Content-Type", "Authorization", "X-Tenant-Id")
                .AllowCredentials();
        }
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

// Add HTTP logging for observability
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
    logging.RequestBodyLogLimit = 4096;
    logging.ResponseBodyLogLimit = 4096;
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

// Enable HTTP request/response logging
app.UseHttpLogging();

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

// Health check endpoints (anonymous)
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

app.Run();
