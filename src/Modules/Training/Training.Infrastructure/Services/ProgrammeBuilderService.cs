using System.Text.Json;
using Common.Core;
using Microsoft.EntityFrameworkCore;
using Training.Application.Services;
using Training.Domain.Documents;
using Training.Domain.Entities;
using Training.Domain.Enums;
using Training.Infrastructure.Persistence;

namespace Training.Infrastructure.Services;

public sealed class ProgrammeBuilderService : IProgrammeBuilderService
{
    private readonly TrainingDbContext _dbContext;
    private readonly VectorSearchService _vectorSearchService;
    private readonly IProgrammeDocumentStore _documentStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILlmService _llmService;
    private readonly IProgrammeService _programmeService;
    private readonly ITenantContext _tenantContext;

    public ProgrammeBuilderService(
        TrainingDbContext dbContext,
        VectorSearchService vectorSearchService,
        IProgrammeDocumentStore documentStore,
        IEmbeddingService embeddingService,
        ILlmService llmService,
        IProgrammeService programmeService,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _vectorSearchService = vectorSearchService;
        _documentStore = documentStore;
        _embeddingService = embeddingService;
        _llmService = llmService;
        _programmeService = programmeService;
        _tenantContext = tenantContext;
    }

    public async Task<BuilderSessionResult> StartSessionAsync(
        Guid gymnastId,
        string goals,
        string ragScope = "gymnast",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(goals))
            throw new ArgumentException("Goals cannot be empty", nameof(goals));

        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId is required");
        var coachId = Guid.NewGuid(); // TODO: Get from auth context

        // Generate embedding for goals
        var goalEmbedding = await _embeddingService.GenerateEmbeddingAsync(goals, cancellationToken);

        // Vector search for similar programmes
        var similarProgrammes = ragScope == "gymnast"
            ? await _vectorSearchService.SearchSimilarProgrammesAsync(goalEmbedding, 5, gymnastId, cancellationToken)
            : await _vectorSearchService.SearchSimilarProgrammesAsync(goalEmbedding, 5, null, cancellationToken);

        // Fetch full documents from CouchDB
        var documentsList = similarProgrammes.Any()
            ? await _documentStore.BulkGetAsync(similarProgrammes.Select(p => p.CouchDbDocId), cancellationToken)
            : [];
        var documents = documentsList.Where(d => d is not null).Cast<ProgrammeDocument>().ToList();

        // Build RAG context
        var context = BuildRagContext(documents);

        // Generate LLM suggestion
        var systemPrompt = BuildSystemPrompt(context);
        var suggestion = await _llmService.GenerateAsync(systemPrompt, goals, null, cancellationToken);

        // Create and save session
        var session = ProgrammeBuilderSession.Create(tenantId, coachId, gymnastId, goals, ragScope);
        session.AppendConversation(goals, suggestion);

        _dbContext.ProgrammeBuilderSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new BuilderSessionResult(session.Id, suggestion);
    }

    public async Task<BuilderSessionResult> ContinueSessionAsync(
        Guid sessionId,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty", nameof(message));

        var session = await _dbContext.ProgrammeBuilderSessions.FindAsync([sessionId], cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found");

        if (session.Status != SessionStatus.Active)
            throw new InvalidOperationException($"Session {sessionId} is not active");

        // Parse conversation history
        var history = ParseConversationHistory(session.ConversationHistoryJson);

        // Re-generate embedding and fetch similar programmes for updated context
        var goalEmbedding = await _embeddingService.GenerateEmbeddingAsync(session.InitialGoals, cancellationToken);
        var gymnastIdFilter = session.RagScopeConfig == "gymnast" ? (Guid?)session.GymnastId : null;
        var similarProgrammes = await _vectorSearchService.SearchSimilarProgrammesAsync(
            goalEmbedding, 5, gymnastIdFilter, cancellationToken);

        var documentsList = similarProgrammes.Any()
            ? await _documentStore.BulkGetAsync(similarProgrammes.Select(p => p.CouchDbDocId), cancellationToken)
            : [];
        var documents = documentsList.Where(d => d is not null).Cast<ProgrammeDocument>().ToList();

        var context = BuildRagContext(documents);
        var systemPrompt = BuildSystemPrompt(context);

        // Generate response with conversation history
        var suggestion = await _llmService.GenerateAsync(systemPrompt, message, history, cancellationToken);

        // Update session
        session.AppendConversation(message, suggestion);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new BuilderSessionResult(session.Id, suggestion);
    }

    public async Task<Guid> AcceptSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.ProgrammeBuilderSessions.FindAsync([sessionId], cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found");

        if (session.Status != SessionStatus.Active)
            throw new InvalidOperationException($"Session {sessionId} is not active");

        // Parse final LLM suggestion into ProgrammeDocument
        var history = ParseConversationHistory(session.ConversationHistoryJson);
        var lastAssistantMessage = history.LastOrDefault(m => m.Role == "assistant")?.Content
            ?? throw new InvalidOperationException("No programme suggestion found in session");

        var programmeDocument = BuildProgrammeDocument(session, lastAssistantMessage);

        // Create programme (dual-write: CouchDB + PostgreSQL)
        var metadata = await _programmeService.CreateAsync(programmeDocument, cancellationToken);

        // Complete session
        session.Complete(metadata.Id);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return metadata.Id;
    }

    private static string BuildRagContext(IReadOnlyList<ProgrammeDocument> documents)
    {
        if (!documents.Any())
            return "No similar programmes found in history.";

        var contextParts = new List<string>();
        foreach (var doc in documents)
        {
            contextParts.Add($"Programme: {doc.Title}");
            contextParts.Add($"Goals: {doc.Goals}");
            if (doc.Content?.Weeks is not null)
            {
                contextParts.Add($"Duration: {doc.Content.Weeks.Count} weeks");
                foreach (var week in doc.Content.Weeks.Take(2)) // First 2 weeks for context
                {
                    contextParts.Add($"  Week {week.WeekNumber}: {week.Focus}");
                }
            }
            contextParts.Add("");
        }

        return string.Join("\n", contextParts);
    }

    private static string BuildSystemPrompt(string context)
    {
        return $@"You are an expert gymnastics coach assistant. Your role is to create personalized training programmes based on the gymnast's goals and past training history.

Similar programmes from history:
{context}

When creating a programme:
- Consider the gymnast's past training patterns shown above
- Structure the response as a detailed weekly training plan
- Include specific exercises with sets, reps, and progression notes
- Focus on safe progression and injury prevention
- Be specific and actionable

Keep your response focused and practical.";
    }

    private static List<ConversationMessage> ParseConversationHistory(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        var turns = JsonSerializer.Deserialize<List<ConversationTurn>>(json) ?? [];

        var messages = new List<ConversationMessage>();
        foreach (var turn in turns)
        {
            messages.Add(new ConversationMessage("user", turn.UserMessage));
            messages.Add(new ConversationMessage("assistant", turn.AssistantResponse));
        }

        return messages;
    }

    private static ProgrammeDocument BuildProgrammeDocument(ProgrammeBuilderSession session, string suggestion)
    {
        // For MVP, create a basic programme structure from the LLM text
        // Future enhancement: Parse LLM output into structured WeekContent/ExerciseContent
        return new ProgrammeDocument
        {
            Id = $"programme-{Guid.NewGuid()}",
            TenantId = session.TenantId,
            GymnastId = session.GymnastId,
            CoachId = session.CoachId,
            Title = $"Programme for {session.InitialGoals}",
            Goals = session.InitialGoals,
            Content = new ProgrammeContent
            {
                Weeks =
                [
                    new WeekContent
                    {
                        WeekNumber = 1,
                        Focus = "Initial training based on goals",
                        Notes = suggestion
                    }
                ]
            },
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddMonths(1)
        };
    }
}
