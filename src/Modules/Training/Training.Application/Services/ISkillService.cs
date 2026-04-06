using Common.Core;
using Training.Domain.Entities;
using Training.Domain.Enums;

namespace Training.Application.Services;

/// <summary>
/// Service for managing the global skills catalog with semantic search capabilities.
/// Skills are NOT tenant-scoped - they are shared across all tenants.
/// </summary>
public interface ISkillService
{
    /// <summary>
    /// Creates a new skill in the global catalog.
    /// Generates embedding from title and description for semantic search.
    /// </summary>
    /// <param name="title">Skill title</param>
    /// <param name="description">Detailed skill description</param>
    /// <param name="effectivenessRating">Effectiveness rating (1-5)</param>
    /// <param name="sections">Gymnastics sections this skill applies to</param>
    /// <param name="tenantId">Tenant ID of creator (for attribution)</param>
    /// <param name="userId">User ID of creator (for attribution)</param>
    /// <param name="imageUrl">Optional image URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created skill or error</returns>
    Task<Result<Skill>> CreateAsync(
        string title,
        string description,
        int effectivenessRating,
        IReadOnlyList<GymnasticSection> sections,
        Guid tenantId,
        Guid userId,
        string? imageUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing skill.
    /// Regenerates embedding if title or description changed.
    /// </summary>
    /// <param name="id">Skill ID</param>
    /// <param name="title">Updated title</param>
    /// <param name="description">Updated description</param>
    /// <param name="effectivenessRating">Updated effectiveness rating (1-5)</param>
    /// <param name="sections">Updated gymnastics sections</param>
    /// <param name="imageUrl">Updated image URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated skill or error</returns>
    Task<Result<Skill>> UpdateAsync(
        Guid id,
        string title,
        string description,
        int effectivenessRating,
        IReadOnlyList<GymnasticSection> sections,
        string? imageUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a skill by ID.
    /// </summary>
    /// <param name="id">Skill ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Skill or NotFound error</returns>
    Task<Result<Skill>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists skills with optional filtering and pagination.
    /// </summary>
    /// <param name="section">Optional gymnastics section filter</param>
    /// <param name="minRating">Optional minimum effectiveness rating filter</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Page size (max 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated skill list</returns>
    Task<Result<SkillListResult>> ListAsync(
        GymnasticSection? section = null,
        int? minRating = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs semantic search on skills using vector similarity.
    /// </summary>
    /// <param name="query">Search query (natural language)</param>
    /// <param name="maxResults">Maximum number of results (default 10, max 50)</param>
    /// <param name="section">Optional gymnastics section filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of skills ranked by similarity score</returns>
    Task<Result<IReadOnlyList<SkillSearchResult>>> SearchAsync(
        string query,
        int maxResults = 10,
        GymnasticSection? section = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a skill from the catalog.
    /// Fails with Conflict error if skill is in use (UsageCount > 0).
    /// </summary>
    /// <param name="id">Skill ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success or error</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the usage count for a skill.
    /// Called when a skill is added to a programme.
    /// </summary>
    /// <param name="id">Skill ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success or error</returns>
    Task<Result> IncrementUsageAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrements the usage count for a skill.
    /// Called when a skill is removed from a programme.
    /// </summary>
    /// <param name="id">Skill ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success or error</returns>
    Task<Result> DecrementUsageAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Paginated skill list result.
/// </summary>
public sealed record SkillListResult(
    IReadOnlyList<Skill> Skills,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Skill search result with similarity score.
/// </summary>
public sealed record SkillSearchResult(
    Skill Skill,
    double SimilarityScore);
