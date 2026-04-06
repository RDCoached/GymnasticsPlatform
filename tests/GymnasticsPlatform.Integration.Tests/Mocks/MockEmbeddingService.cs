using Training.Application.Services;

namespace GymnasticsPlatform.Integration.Tests.Mocks;

public sealed class MockEmbeddingService : IEmbeddingService
{
    private static readonly Random _random = new(42); // Fixed seed for deterministic tests

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        // Generate a deterministic vector based on text hash for consistent tests
        // Use text hash as seed to get same vector for same text
        var seed = text.GetHashCode();
        var rng = new Random(seed);

        var embedding = new float[384];
        for (int i = 0; i < 384; i++)
        {
            embedding[i] = (float)(rng.NextDouble() * 2 - 1); // Values between -1 and 1
        }

        return Task.FromResult(embedding);
    }
}
