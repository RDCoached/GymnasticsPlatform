using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Training.Infrastructure.Tests.Fixtures;

public sealed class CouchDbFixture : IAsyncLifetime
{
    private readonly IContainer _container;
    private const string Username = "admin";
    private const string Password = "testpass";
    private const string DatabaseName = "programmes_test";

    public string ServerUrl { get; private set; } = string.Empty;
    public string Database => DatabaseName;
    public string User => Username;
    public string Pass => Password;

    public CouchDbFixture()
    {
        _container = new ContainerBuilder()
            .WithImage("couchdb:3.4")
            .WithPortBinding(5984, true)
            .WithEnvironment("COUCHDB_USER", Username)
            .WithEnvironment("COUCHDB_PASSWORD", Password)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5984))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var port = _container.GetMappedPublicPort(5984);
        ServerUrl = $"http://localhost:{port}";

        using var httpClient = new HttpClient();
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{Username}:{Password}"));
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

        var maxRetries = 10;
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                var response = await httpClient.GetAsync($"{ServerUrl}/_up");
                if (response.IsSuccessStatusCode)
                    break;
            }
            catch
            {
                if (i == maxRetries - 1)
                    throw;
                await Task.Delay(1000);
            }
        }

        var createDbResponse = await httpClient.PutAsync($"{ServerUrl}/{DatabaseName}", null);
        if (createDbResponse.StatusCode != System.Net.HttpStatusCode.PreconditionFailed)
        {
            createDbResponse.EnsureSuccessStatusCode();
        }
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
