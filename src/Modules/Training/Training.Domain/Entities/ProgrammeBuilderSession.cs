using System.Text.Json;
using Common.Core;
using Training.Domain.Enums;

namespace Training.Domain.Entities;

/// <summary>
/// Tracks a RAG-powered programme building conversation between coach and AI.
/// Stores conversation history and resulting programme reference.
/// </summary>
public sealed class ProgrammeBuilderSession : IMultiTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CoachId { get; private set; }
    public Guid GymnastId { get; private set; }
    public string InitialGoals { get; private set; } = string.Empty;
    public string? ConversationHistoryJson { get; private set; }
    public Guid? ResultingProgrammeId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public SessionStatus Status { get; private set; }
    public string RagScopeConfig { get; private set; } = string.Empty;

    private ProgrammeBuilderSession() { } // EF Core

    public static ProgrammeBuilderSession Create(
        Guid tenantId,
        Guid coachId,
        Guid gymnastId,
        string initialGoals,
        string ragScopeConfig)
    {
        if (string.IsNullOrWhiteSpace(initialGoals))
            throw new ArgumentException("Initial goals are required", nameof(initialGoals));

        return new ProgrammeBuilderSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CoachId = coachId,
            GymnastId = gymnastId,
            InitialGoals = initialGoals,
            RagScopeConfig = ragScopeConfig,
            Status = SessionStatus.Active,
            StartedAt = DateTimeOffset.UtcNow,
            ConversationHistoryJson = "[]"
        };
    }

    public void AppendConversation(string userMessage, string assistantResponse)
    {
        var history = string.IsNullOrWhiteSpace(ConversationHistoryJson)
            ? new List<ConversationTurn>()
            : JsonSerializer.Deserialize<List<ConversationTurn>>(ConversationHistoryJson) ?? [];

        history.Add(new ConversationTurn
        {
            UserMessage = userMessage,
            AssistantResponse = assistantResponse,
            Timestamp = DateTimeOffset.UtcNow
        });

        ConversationHistoryJson = JsonSerializer.Serialize(history);
    }

    public void Complete(Guid programmeId)
    {
        if (Status != SessionStatus.Active)
            throw new InvalidOperationException("Session is not active");

        Status = SessionStatus.Completed;
        ResultingProgrammeId = programmeId;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Abandon()
    {
        if (Status != SessionStatus.Active)
            throw new InvalidOperationException("Session is not active");

        Status = SessionStatus.Abandoned;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}

public sealed record ConversationTurn
{
    public string UserMessage { get; init; } = string.Empty;
    public string AssistantResponse { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
}
