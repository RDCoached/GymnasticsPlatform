using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Training.Infrastructure.Tests.Fixtures;

public sealed class OllamaFixture : IAsyncLifetime
{
    private IContainer? _container;

    public string BaseUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder()
            .WithImage("ollama/ollama:latest")
            .WithPortBinding(11434, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(11434)
                    .ForPath("/api/tags")))
            .Build();

        await _container.StartAsync();

        var port = _container.GetMappedPublicPort(11434);
        BaseUrl = $"http://localhost:{port}";

        // Pull the embedding model
        await PullModelAsync("all-minilm:l6-v2");
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private async Task PullModelAsync(string modelName)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var pullRequest = new
        {
            name = modelName,
            stream = false
        };

        var response = await httpClient.PostAsJsonAsync(
            $"{BaseUrl}/api/pull",
            pullRequest);

        response.EnsureSuccessStatusCode();
    }
}
