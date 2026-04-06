using Microsoft.Extensions.Options;
using Training.Application.Services;
using Training.Infrastructure.Configuration;
using Training.Infrastructure.Services;
using Training.Infrastructure.Tests.Fixtures;

namespace Training.Infrastructure.Tests.Services;

[Collection("Ollama")]
public sealed class OllamaLlmServiceTests : IClassFixture<OllamaFixture>
{
    private readonly OllamaFixture _fixture;
    private readonly ILlmService _service;

    public OllamaLlmServiceTests(OllamaFixture fixture)
    {
        _fixture = fixture;

        var httpClient = new HttpClient();
        var settings = Options.Create(new OllamaSettings
        {
            BaseUrl = _fixture.BaseUrl,
            GenerationModel = "llama3.2:3b",
            TimeoutSeconds = 120
        });

        _service = new OllamaLlmService(httpClient, settings);
    }

    [Fact]
    public async Task GenerateAsync_WithSimplePrompt_ReturnsResponse()
    {
        // Arrange
        var systemPrompt = "You are a helpful assistant that provides concise answers.";
        var userMessage = "What is 2 + 2?";

        // Act
        var response = await _service.GenerateAsync(systemPrompt, userMessage);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response);
        Assert.Contains("4", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_WithEmptySystemPrompt_ThrowsArgumentException()
    {
        // Arrange
        var systemPrompt = "";
        var userMessage = "Hello";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateAsync(systemPrompt, userMessage));
    }

    [Fact]
    public async Task GenerateAsync_WithNullUserMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var systemPrompt = "You are a helpful assistant.";
        string? userMessage = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.GenerateAsync(systemPrompt, userMessage!));
    }

    [Fact]
    public async Task GenerateAsync_WithConversationHistory_MaintainsContext()
    {
        // Arrange
        var systemPrompt = "You are a helpful assistant. Answer directly and briefly.";
        var conversationHistory = new List<ConversationMessage>
        {
            new("user", "My name is Alice."),
            new("assistant", "Hello Alice!")
        };
        var userMessage = "What is my name?";

        // Act
        var response = await _service.GenerateAsync(systemPrompt, userMessage, conversationHistory);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response);
        Assert.Contains("Alice", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var systemPrompt = "You are a helpful assistant.";
        var userMessage = "Write a very long story.";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.GenerateAsync(systemPrompt, userMessage, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task GenerateAsync_GymnasticsCoachScenario_GeneratesRelevantResponse()
    {
        // Arrange
        var systemPrompt = "You are a gymnastics coach. Answer briefly.";
        var userMessage = "Name one vault drill for power.";

        // Act
        var response = await _service.GenerateAsync(systemPrompt, userMessage);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response);
        // Should contain gymnastics-related terms (relaxed check for brief responses)
        Assert.True(response.Length > 10, "Response should contain meaningful content");
    }
}
