using Microsoft.EntityFrameworkCore;
using Pgvector;
using Training.Domain.Entities;
using Training.Domain.Enums;
using Training.Infrastructure.Persistence;

namespace Training.Infrastructure.Services;

public sealed class VectorSearchService
{
    private readonly TrainingDbContext _dbContext;

    public VectorSearchService(TrainingDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<ProgrammeMetadata>> SearchSimilarProgrammesAsync(
        float[] queryEmbedding,
        int maxResults,
        Guid? gymnastId = null,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding is null)
            throw new ArgumentNullException(nameof(queryEmbedding));

        if (queryEmbedding.Length != 384)
            throw new ArgumentException("Query embedding must be 384 dimensions", nameof(queryEmbedding));

        if (maxResults <= 0)
            throw new ArgumentException("Max results must be greater than zero", nameof(maxResults));

        var queryVector = new Vector(queryEmbedding);

        // Build WHERE clause conditions
        var statusCondition = $"status IN ({(int)ProgrammeStatus.Active}, {(int)ProgrammeStatus.Completed})";
        var embeddingCondition = "embedding_vector IS NOT NULL";
        var gymnastCondition = gymnastId.HasValue ? $"AND gymnast_id = '{gymnastId.Value}'" : "";

        // Use raw SQL for vector similarity search with pgvector <=> operator
        // Note: Tenant filtering is applied automatically via global query filter
        var sql = $@"
            SELECT * FROM programme_metadata
            WHERE {statusCondition}
              AND {embeddingCondition}
              {gymnastCondition}
            ORDER BY embedding_vector <=> '{queryVector}'::vector
            LIMIT {maxResults}";

        var results = await _dbContext.ProgrammeMetadata
            .FromSqlRaw(sql)
            .ToListAsync(cancellationToken);

        return results;
    }
}
