using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Training.Application.Services;
using Training.Infrastructure.Configuration;

namespace Training.Infrastructure.Services;

public sealed class OllamaLlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;

    public OllamaLlmService(HttpClient httpClient, IOptions<OllamaSettings> settings)
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

    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        IReadOnlyList<ConversationMessage>? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        if (systemPrompt is null)
            throw new ArgumentNullException(nameof(systemPrompt));

        if (string.IsNullOrWhiteSpace(systemPrompt))
            throw new ArgumentException("System prompt cannot be empty or whitespace", nameof(systemPrompt));

        if (userMessage is null)
            throw new ArgumentNullException(nameof(userMessage));

        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException("User message cannot be empty or whitespace", nameof(userMessage));

        // Build messages array for conversation
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        // Add conversation history if provided
        if (conversationHistory is not null)
        {
            foreach (var message in conversationHistory)
            {
                messages.Add(new ChatMessage { Role = message.Role, Content = message.Content });
            }
        }

        // Add current user message
        messages.Add(new ChatMessage { Role = "user", Content = userMessage });

        var request = new ChatRequest
        {
            Model = _settings.GenerationModel,
            Messages = messages,
            Stream = false
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/api/chat")
        {
            Content = JsonContent.Create(request, options: _jsonOptions)
        };

        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Ollama generation request failed with status {response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(_jsonOptions, cancellationToken);

        if (result?.Message?.Content is null)
        {
            throw new InvalidOperationException("Ollama returned no response content");
        }

        return result.Message.Content;
    }

    private sealed class ChatRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required List<ChatMessage> Messages { get; init; }

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public required string Content { get; init; }
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("message")]
        public ChatMessage? Message { get; init; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; init; }

        [JsonPropertyName("done")]
        public bool Done { get; init; }
    }
}
