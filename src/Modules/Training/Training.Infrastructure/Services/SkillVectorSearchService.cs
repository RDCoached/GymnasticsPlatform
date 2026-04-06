using Microsoft.EntityFrameworkCore;
using Pgvector;
using Training.Application.Services;
using Training.Domain.Entities;
using Training.Domain.Enums;
using Training.Infrastructure.Persistence;

namespace Training.Infrastructure.Services;

/// <summary>
/// Vector similarity search service for skills catalog.
/// Skills are NOT tenant-scoped (globally accessible).
/// </summary>
public sealed class SkillVectorSearchService
{
    private readonly TrainingDbContext _dbContext;

    public SkillVectorSearchService(TrainingDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Performs vector similarity search on skills using cosine distance.
    /// Returns skills ordered by similarity score (most similar first).
    /// </summary>
    /// <param name="queryEmbedding">Query embedding vector (384 dimensions)</param>
    /// <param name="maxResults">Maximum number of results</param>
    /// <param name="section">Optional gymnastics section filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of skill search results with similarity scores</returns>
    public async Task<IReadOnlyList<SkillSearchResult>> SearchSimilarSkillsAsync(
        float[] queryEmbedding,
        int maxResults,
        GymnasticSection? section = null,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding is null)
            throw new ArgumentNullException(nameof(queryEmbedding));

        if (queryEmbedding.Length != 384)
            throw new ArgumentException("Query embedding must be 384 dimensions", nameof(queryEmbedding));

        if (maxResults <= 0)
            throw new ArgumentException("Max results must be greater than zero", nameof(maxResults));

        var queryVector = new Vector(queryEmbedding);

        // Build section filter as EXISTS subquery
        var sectionFilter = section.HasValue
            ? $@"AND EXISTS (
                    SELECT 1 FROM skill_sections ss
                    WHERE ss.skill_id = skills.id
                    AND ss.section = {(int)section.Value}
                 )"
            : "";

        // Use raw SQL for vector similarity search with pgvector <=> operator
        // Similarity score: 1 - cosine_distance (converts distance to similarity in [0, 1] range)
        // Note: Skills are NOT tenant-filtered (global catalog)
        var sql = $@"
            SELECT
                *,
                (1 - (embedding_vector <=> '{queryVector}'::vector)) as similarity_score
            FROM skills
            WHERE embedding_vector IS NOT NULL
            {sectionFilter}
            ORDER BY embedding_vector <=> '{queryVector}'::vector
            LIMIT {maxResults}";

        // Execute raw SQL to get skills with similarity scores
        var connection = _dbContext.Database.GetDbConnection();
        await _dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            // First, collect all skill IDs and similarity scores
            var skillScores = new List<(Guid SkillId, double SimilarityScore)>();

            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var skillId = reader.GetGuid(reader.GetOrdinal("id"));
                    var similarityScore = reader.GetDouble(reader.GetOrdinal("similarity_score"));
                    skillScores.Add((skillId, similarityScore));
                }
            } // Reader is disposed here

            // Now load the full skill entities with sections
            var results = new List<SkillSearchResult>();
            foreach (var (skillId, similarityScore) in skillScores)
            {
                var skill = await _dbContext.Skills
                    .Include(s => s.Sections)
                    .FirstAsync(s => s.Id == skillId, cancellationToken);

                results.Add(new SkillSearchResult(skill, similarityScore));
            }

            return results;
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }
    }
}
