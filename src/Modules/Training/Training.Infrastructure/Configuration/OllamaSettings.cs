namespace Training.Infrastructure.Configuration;

public sealed class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "all-minilm:l6-v2";
    public string GenerationModel { get; set; } = "llama3.2:3b";
    public int TimeoutSeconds { get; set; } = 120;
}
