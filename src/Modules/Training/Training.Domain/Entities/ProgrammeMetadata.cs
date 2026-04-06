using Common.Core;
using Training.Domain.Enums;

namespace Training.Domain.Entities;

/// <summary>
/// PostgreSQL entity storing programme metadata and embeddings.
/// Points to the full document in CouchDB via CouchDbDocId.
/// </summary>
public sealed class ProgrammeMetadata : IMultiTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid GymnastId { get; private set; }
    public Guid CoachId { get; private set; }
    public string CouchDbDocId { get; private set; } = string.Empty;
    public string CouchDbRev { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public ProgrammeStatus Status { get; private set; }
    public DateTimeOffset StartDate { get; private set; }
    public DateTimeOffset EndDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastModifiedAt { get; private set; }
    public float[]? EmbeddingVector { get; private set; }

    private ProgrammeMetadata() { } // EF Core

    public static ProgrammeMetadata Create(
        Guid tenantId,
        Guid gymnastId,
        Guid coachId,
        string couchDbDocId,
        string couchDbRev,
        string title,
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (endDate < startDate)
            throw new ArgumentException("End date must be after start date", nameof(endDate));

        var now = DateTimeOffset.UtcNow;

        return new ProgrammeMetadata
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            GymnastId = gymnastId,
            CoachId = coachId,
            CouchDbDocId = couchDbDocId,
            CouchDbRev = couchDbRev,
            Title = title,
            Status = ProgrammeStatus.Draft,
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = now,
            LastModifiedAt = now
        };
    }

    public void Activate()
    {
        Status = ProgrammeStatus.Active;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void Complete()
    {
        Status = ProgrammeStatus.Completed;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void Archive()
    {
        Status = ProgrammeStatus.Archived;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateCouchDbRev(string newRev)
    {
        if (string.IsNullOrWhiteSpace(newRev))
            throw new ArgumentException("Revision is required", nameof(newRev));

        CouchDbRev = newRev;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void SetEmbedding(float[] embedding)
    {
        if (embedding == null || embedding.Length != 384)
            throw new ArgumentException("Embedding must be exactly 384 dimensions", nameof(embedding));

        EmbeddingVector = embedding;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }
}
