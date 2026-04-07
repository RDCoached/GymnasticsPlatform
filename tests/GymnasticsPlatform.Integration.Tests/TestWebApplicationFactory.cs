using Auth.Application.Services;
using Auth.Infrastructure.Persistence;
using Common.Core;
using GymnasticsPlatform.Integration.Tests.Mocks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Training.Infrastructure.Persistence;
using Pgvector.EntityFrameworkCore;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string OnboardingTenantId = "00000000-0000-0000-0000-000000000001";

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("gymnastics_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    private TestTenantContext? _testTenantContext;
    public TestTenantContext TestTenantContext => _testTenantContext
        ?? throw new InvalidOperationException("TestTenantContext not initialized. CreateClient must be called first.");

    private readonly MockKeycloakAdminService _mockKeycloakService = new();
    public MockKeycloakAdminService MockKeycloakService => _mockKeycloakService;

    private readonly TestEmailService _testEmailService = new();
    public TestEmailService TestEmailService => _testEmailService;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureTestServices(services =>
        {
            // Remove the existing DbContext registrations
            services.RemoveAll(typeof(DbContextOptions<AuthDbContext>));
            services.RemoveAll(typeof(AuthDbContext));
            services.RemoveAll(typeof(DbContextOptions<TrainingDbContext>));
            services.RemoveAll(typeof(TrainingDbContext));

            // Add DbContexts with test container connection string
            services.AddDbContext<AuthDbContext>(options =>
                options.UseNpgsql(_dbContainer.GetConnectionString()));

            services.AddDbContext<TrainingDbContext>(options =>
                options.UseNpgsql(
                    _dbContainer.GetConnectionString(),
                    o => o.UseVector()));

            // Replace IKeycloakAdminService with mock
            services.RemoveAll(typeof(IKeycloakAdminService));
            services.AddSingleton<IKeycloakAdminService>(_mockKeycloakService);

            // Replace IEmailService with test implementation
            services.RemoveAll(typeof(IEmailService));
            services.AddSingleton<IEmailService>(_testEmailService);

            // Replace IEmbeddingService with mock
            services.RemoveAll(typeof(Training.Application.Services.IEmbeddingService));
            services.AddSingleton<Training.Application.Services.IEmbeddingService>(new MockEmbeddingService());

            // Replace ITenantContext with test implementation
            services.RemoveAll(typeof(ITenantContext));
            services.AddSingleton<ITenantContext>(sp =>
            {
                var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
                _testTenantContext = new TestTenantContext(httpContextAccessor);
                return _testTenantContext;
            });

            // Add test authentication scheme alongside existing JWT bearer
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                // Make test scheme the default if it's registered
                if (options.Schemes.Any(s => s.Name == TestAuthenticationHandler.AuthenticationScheme))
                {
                    options.DefaultScheme = TestAuthenticationHandler.AuthenticationScheme;
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = "Bearer"; // Keep Bearer for challenge to get WWW-Authenticate header
                }
            });

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.AuthenticationScheme,
                    options => { });
        });
    }

    public override IServiceProvider Services
    {
        get
        {
            EnsureDatabaseMigrated();
            EnsureTenantContextInitialized();
            return base.Services;
        }
    }

    private bool _databaseMigrated;
    private bool _tenantContextInitialized;
    private readonly object _migrationLock = new();
    private readonly object _tenantContextLock = new();

    private void EnsureDatabaseMigrated()
    {
        if (_databaseMigrated) return;

        lock (_migrationLock)
        {
            if (_databaseMigrated) return;

            using var scope = base.Services.CreateScope();

            // Migrate AuthDbContext
            var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            authDb.Database.Migrate();

            // Migrate TrainingDbContext
            var trainingDb = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
            trainingDb.Database.Migrate();

            _databaseMigrated = true;
        }
    }

    private void EnsureTenantContextInitialized()
    {
        if (_tenantContextInitialized) return;

        lock (_tenantContextLock)
        {
            if (_tenantContextInitialized) return;

            // Force ITenantContext singleton to be resolved, which initializes _testTenantContext
            using var scope = base.Services.CreateScope();
            _ = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            _tenantContextInitialized = true;
        }
    }

    public HttpClient CreateAuthenticatedClient(string userId, string tenantId, string email = "test@example.com", string username = "testuser")
    {
        // Ensure database is migrated and services are initialized
        EnsureDatabaseMigrated();
        EnsureTenantContextInitialized();

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-Test-Tenant-Id", tenantId);
        client.DefaultRequestHeaders.Add("X-Test-Email", email);
        client.DefaultRequestHeaders.Add("X-Test-Username", username);
        return client;
    }

    public new HttpClient CreateClient(WebApplicationFactoryClientOptions? options = null)
    {
        // Ensure database is migrated and tenant context initialized before creating client
        EnsureDatabaseMigrated();
        EnsureTenantContextInitialized();
        return base.CreateClient(options ?? new WebApplicationFactoryClientOptions());
    }

    public HttpClient CreateAuthenticatedUserClient(string userId, Guid tenantId)
    {
        return CreateAuthenticatedClient(userId, tenantId.ToString());
    }

    public HttpClient CreateOnboardingUserClient(string userId)
    {
        return CreateAuthenticatedClient(userId, OnboardingTenantId);
    }

    public void ResetMockServices()
    {
        _mockKeycloakService.Reset();
        _testEmailService.SentEmails.Clear();
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        _mockKeycloakService.Reset();
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
