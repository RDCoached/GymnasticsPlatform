namespace Training.Application.Services;

/// <summary>
/// Service for generating embeddings from text using AI models.
/// Abstracts the embedding provider (Ollama, OpenAI, etc.).
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates an embedding vector from the provided text.
    /// </summary>
    /// <param name="text">Text to generate embedding from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Embedding vector (384 dimensions for all-minilm:l6-v2)</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
