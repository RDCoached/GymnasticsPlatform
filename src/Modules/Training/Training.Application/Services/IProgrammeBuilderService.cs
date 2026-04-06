namespace Training.Application.Services;

/// <summary>
/// Service for building training programmes through RAG-powered conversations.
/// Orchestrates vector search, document retrieval, and LLM generation.
/// </summary>
public interface IProgrammeBuilderService
{
    /// <summary>
    /// Starts a new programme builder session with initial goals.
    /// Performs semantic search, retrieves relevant programmes, and generates initial suggestion.
    /// </summary>
    /// <param name="gymnastId">The gymnast for whom the programme is being created</param>
    /// <param name="goals">Initial training goals description</param>
    /// <param name="ragScope">RAG scope: "gymnast" (only this gymnast's programmes) or "tenant" (all tenant programmes)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Session ID and initial programme suggestion</returns>
    Task<BuilderSessionResult> StartSessionAsync(
        Guid gymnastId,
        string goals,
        string ragScope = "gymnast",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Continues an existing builder session with a new message.
    /// Maintains conversation history and generates contextual responses.
    /// </summary>
    /// <param name="sessionId">The session ID from StartSessionAsync</param>
    /// <param name="message">User's message/feedback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated programme suggestion</returns>
    Task<BuilderSessionResult> ContinueSessionAsync(
        Guid sessionId,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts the current suggestion and creates the programme.
    /// Performs dual-write: CouchDB (document) + PostgreSQL (metadata with embedding).
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created programme metadata ID</returns>
    Task<Guid> AcceptSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from a builder session operation.
/// </summary>
/// <param name="SessionId">The session ID</param>
/// <param name="Suggestion">The LLM-generated programme suggestion</param>
public sealed record BuilderSessionResult(Guid SessionId, string Suggestion);
