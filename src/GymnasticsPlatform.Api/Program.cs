using Auth.Infrastructure.Persistence;
using Training.Infrastructure.Persistence;
using Pgvector.EntityFrameworkCore;
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

// Add Distributed Cache (Redis for session storage)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
});

// Add Session Service
builder.Services.AddScoped<ISessionService, SessionService>();

// Session authentication configuration removed - now using Entra ID only

// Add User Tenant Service
builder.Services.AddScoped<Auth.Application.Services.IUserTenantService, Auth.Infrastructure.Services.UserTenantService>();

// Add Role Service
builder.Services.AddScoped<Auth.Application.Services.IRoleService, Auth.Infrastructure.Services.RoleService>();

// Add Audit Service
builder.Services.AddScoped<Auth.Application.Services.IAuditService, Auth.Infrastructure.Services.AuditService>();

// Email Service configuration
builder.Services.Configure<Auth.Infrastructure.Configuration.EmailSettings>(builder.Configuration.GetSection("Email"));

if (builder.Environment.IsDevelopment())
{
    // Use MailHog SMTP in development - emails viewable at http://localhost:8025
    builder.Services.AddScoped<Auth.Application.Services.IEmailService, Auth.Infrastructure.Services.MailHogEmailService>();
}
else
{
    // Use Resend email service in production
    var resendApiKey = builder.Configuration["Resend:ApiKey"];
    builder.Services.AddOptions<ResendClientOptions>()
        .Configure(options => options.ApiToken = resendApiKey ?? throw new InvalidOperationException("Resend:ApiKey is required for production"));
    builder.Services.AddHttpClient<ResendClient>();
    builder.Services.AddTransient<IResend, ResendClient>();
    builder.Services.AddScoped<Auth.Application.Services.IEmailService, Auth.Infrastructure.Services.ResendEmailService>();
}

// Add Authentication Provider (Microsoft Entra External ID)
builder.Services.AddScoped<Auth.Application.Services.IAuthenticationProvider, Auth.Infrastructure.Services.ExternalIdAuthenticationProvider>();
// Add DbContext
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<TrainingDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.UseVector()));

// Training module services
builder.Services.Configure<Training.Infrastructure.Configuration.OllamaSettings>(builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<Training.Infrastructure.Configuration.CouchDbSettings>(builder.Configuration.GetSection("CouchDb"));

builder.Services.AddHttpClient<Training.Application.Services.IEmbeddingService, Training.Infrastructure.Services.OllamaEmbeddingService>();
builder.Services.AddHttpClient<Training.Application.Services.ILlmService, Training.Infrastructure.Services.OllamaLlmService>();

builder.Services.AddScoped<Training.Infrastructure.Services.VectorSearchService>();
builder.Services.AddScoped<Training.Application.Services.IProgrammeDocumentStore>(provider =>
{
    var settings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Training.Infrastructure.Configuration.CouchDbSettings>>();
    var httpClient = new HttpClient();
    return new Training.Infrastructure.DocumentStore.CouchDbProgrammeDocumentStore(httpClient, settings);
});
builder.Services.AddScoped<Training.Application.Services.IProgrammeService, Training.Infrastructure.Services.ProgrammeService>();
builder.Services.AddScoped<Training.Application.Services.IProgrammeBuilderService, Training.Infrastructure.Services.ProgrammeBuilderService>();

// Skills Catalog Service
builder.Services.AddScoped<Training.Application.Services.ISkillService, Training.Infrastructure.Services.SkillService>();
builder.Services.AddScoped<Training.Infrastructure.Seeders.SkillSeeder>();

// Add OpenAPI
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "Gymnastics Platform API";
        document.Info.Version = "v1";
        document.Info.Description = "Multi-tenant gymnastics platform API with Microsoft Entra ID authentication";
        return Task.CompletedTask;
    });
});

// Add Authentication (Microsoft Entra External ID JWT)
// Skip JWT configuration in Test environment - TestWebApplicationFactory provides its own auth scheme
if (!builder.Environment.IsEnvironment("Test"))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var externalIdConfig = builder.Configuration.GetSection("Authentication:ExternalId");
            var authority = externalIdConfig["Authority"] ?? throw new InvalidOperationException("ExternalId Authority not configured");
            var apiClientId = externalIdConfig["ApiClientId"] ?? throw new InvalidOperationException("ExternalId ApiClientId not configured");

            var tenantId = externalIdConfig["TenantId"] ?? throw new InvalidOperationException("ExternalId TenantId not configured");
            options.Authority = $"{authority}/v2.0";
            options.Audience = $"api://{tenantId}/gymnastics-api";
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.MapInboundClaims = false; // Preserve original JWT claim names

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = $"{authority}/v2.0"
            };
        });
}
else
{
    // Test environment uses custom authentication scheme configured by TestWebApplicationFactory
    builder.Services.AddAuthentication();
}

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
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:3001", "http://localhost:3002", "http://localhost:5173"];

        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
            .WithHeaders("Content-Type", "Authorization", "X-Tenant-Id")
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
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health");
            })
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource("GymnasticsPlatform.*")
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
        .AddRuntimeInstrumentation()
        .AddMeter("GymnasticsPlatform.*")
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

    var trainingDb = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
    await trainingDb.Database.MigrateAsync();
}

// Configure the HTTP request pipeline
app.UseExceptionHandler();

app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

app.UseCors();

// Enable HTTP request/response logging
app.UseHttpLogging();
    
// Session authentication (reads session_id cookie and sets httpContext.User)
app.UseMiddleware<SessionAuthenticationMiddleware>();

app.UseAuthentication(); // JWT Bearer authentication for Entra ID
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

// Auto-discover and register all endpoint groups
app.MapEndpoints();

// Health check endpoints (anonymous)
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

app.Run();
