using Microsoft.Extensions.Options;
using Training.Application.Services;
using Training.Infrastructure.Configuration;
using Training.Infrastructure.Services;
using Training.Infrastructure.Tests.Fixtures;

namespace Training.Infrastructure.Tests.Services;

[Collection("Ollama")]
public sealed class OllamaEmbeddingServiceTests : IClassFixture<OllamaFixture>
{
    private readonly OllamaFixture _fixture;
    private readonly IEmbeddingService _service;

    public OllamaEmbeddingServiceTests(OllamaFixture fixture)
    {
        _fixture = fixture;

        var httpClient = new HttpClient();
        var settings = Options.Create(new OllamaSettings
        {
            BaseUrl = _fixture.BaseUrl,
            EmbeddingModel = "all-minilm:l6-v2",
            TimeoutSeconds = 120
        });

        _service = new OllamaEmbeddingService(httpClient, settings);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithValidText_Returns384DimensionVector()
    {
        // Arrange
        var text = "This is a test programme for gymnastics training";

        // Act
        var embedding = await _service.GenerateEmbeddingAsync(text);

        // Assert
        Assert.NotNull(embedding);
        Assert.Equal(384, embedding.Length);
        Assert.All(embedding, value => Assert.True(value >= -1.0f && value <= 1.0f));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyText_ThrowsArgumentException()
    {
        // Arrange
        var text = "";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateEmbeddingAsync(text));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithNullText_ThrowsArgumentNullException()
    {
        // Arrange
        string? text = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.GenerateEmbeddingAsync(text!));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var text = "Test programme content";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.GenerateEmbeddingAsync(text, cts.Token));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SameTextTwice_ReturnsSimilarVectors()
    {
        // Arrange
        var text = "Gymnastics vault training programme";

        // Act
        var embedding1 = await _service.GenerateEmbeddingAsync(text);
        var embedding2 = await _service.GenerateEmbeddingAsync(text);

        // Assert
        Assert.Equal(384, embedding1.Length);
        Assert.Equal(384, embedding2.Length);

        // Embeddings should be very similar (cosine similarity close to 1.0)
        var cosineSimilarity = CalculateCosineSimilarity(embedding1, embedding2);
        Assert.True(cosineSimilarity > 0.99, $"Cosine similarity was {cosineSimilarity}, expected > 0.99");
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_DifferentTexts_ReturnsDifferentVectors()
    {
        // Arrange
        var text1 = "Gymnastics vault training programme";
        var text2 = "Swimming pool maintenance schedule";

        // Act
        var embedding1 = await _service.GenerateEmbeddingAsync(text1);
        var embedding2 = await _service.GenerateEmbeddingAsync(text2);

        // Assert
        Assert.Equal(384, embedding1.Length);
        Assert.Equal(384, embedding2.Length);

        // Embeddings should be different (cosine similarity significantly less than 1.0)
        var cosineSimilarity = CalculateCosineSimilarity(embedding1, embedding2);
        Assert.True(cosineSimilarity < 0.9, $"Cosine similarity was {cosineSimilarity}, expected < 0.9");
    }

    private static float CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have same length");

        var dotProduct = 0.0;
        var magnitudeA = 0.0;
        var magnitudeB = 0.0;

        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        return (float)(dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB)));
    }
}
