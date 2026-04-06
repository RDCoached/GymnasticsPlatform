using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Training.Application.Services;
using Training.Infrastructure.Configuration;

namespace Training.Infrastructure.Services;

public sealed class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;

    public OllamaEmbeddingService(HttpClient httpClient, IOptions<OllamaSettings> settings)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings.Value;

        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty or whitespace", nameof(text));

        var request = new EmbeddingRequest
        {
            Model = _settings.EmbeddingModel,
            Input = text
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/api/embed")
        {
            Content = JsonContent.Create(request, options: _jsonOptions)
        };

        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Ollama embedding request failed with status {response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(_jsonOptions, cancellationToken);

        if (result?.Embeddings is null || result.Embeddings.Length == 0)
        {
            throw new InvalidOperationException("Ollama returned no embeddings");
        }

        var embedding = result.Embeddings[0];

        if (embedding.Length != 384)
        {
            throw new InvalidOperationException(
                $"Expected 384-dimension embedding from all-minilm:l6-v2, but got {embedding.Length} dimensions");
        }

        return embedding;
    }

    private sealed record EmbeddingRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("input")]
        public required string Input { get; init; }
    }

    private sealed record EmbeddingResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("embeddings")]
        public float[][]? Embeddings { get; init; }
    }
}
