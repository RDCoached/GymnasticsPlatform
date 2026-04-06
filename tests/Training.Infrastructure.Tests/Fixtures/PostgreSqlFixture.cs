using DotNet.Testcontainers.Images;
using Testcontainers.PostgreSql;

namespace Training.Infrastructure.Tests.Fixtures;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public PostgreSqlFixture()
    {
        _container = new PostgreSqlBuilder(image: new DockerImage("pgvector/pgvector:pg16"))
            .WithDatabase("training_test")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
