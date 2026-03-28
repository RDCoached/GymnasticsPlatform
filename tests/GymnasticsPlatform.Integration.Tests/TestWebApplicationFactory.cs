using Auth.Infrastructure.Persistence;
using Common.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace GymnasticsPlatform.Integration.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string OnboardingTenantId = "00000000-0000-0000-0000-000000000001";

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("gymnastics_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    private TestTenantContext? _testTenantContext;
    public TestTenantContext TestTenantContext => _testTenantContext
        ?? throw new InvalidOperationException("TestTenantContext not initialized. CreateClient must be called first.");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureTestServices(services =>
        {
            // Remove the existing DbContext registration
            services.RemoveAll(typeof(DbContextOptions<AuthDbContext>));
            services.RemoveAll(typeof(AuthDbContext));

            // Add DbContext with test container connection string
            services.AddDbContext<AuthDbContext>(options =>
                options.UseNpgsql(_dbContainer.GetConnectionString()));

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

            // Ensure database is created and migrated
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            db.Database.Migrate();
        });
    }

    public HttpClient CreateAuthenticatedClient(string userId, string tenantId, string email = "test@example.com", string username = "testuser")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-Test-Tenant-Id", tenantId);
        client.DefaultRequestHeaders.Add("X-Test-Email", email);
        client.DefaultRequestHeaders.Add("X-Test-Username", username);
        return client;
    }

    public HttpClient CreateAuthenticatedUserClient(string userId, Guid tenantId)
    {
        return CreateAuthenticatedClient(userId, tenantId.ToString());
    }

    public HttpClient CreateOnboardingUserClient(string userId)
    {
        return CreateAuthenticatedClient(userId, OnboardingTenantId);
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
