namespace Training.Application.Services;

/// <summary>
/// Service for generating text using Large Language Models.
/// Abstracts the LLM provider (Ollama, OpenAI, etc.).
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Generates a response from the LLM based on a system prompt and user message.
    /// </summary>
    /// <param name="systemPrompt">System prompt that sets the context/role for the LLM</param>
    /// <param name="userMessage">The user's message/query</param>
    /// <param name="conversationHistory">Optional conversation history for multi-turn dialogues</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated response text</returns>
    Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        IReadOnlyList<ConversationMessage>? conversationHistory = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a message in a conversation.
/// </summary>
/// <param name="Role">The role of the message sender (system, user, assistant)</param>
/// <param name="Content">The message content</param>
public sealed record ConversationMessage(string Role, string Content);
