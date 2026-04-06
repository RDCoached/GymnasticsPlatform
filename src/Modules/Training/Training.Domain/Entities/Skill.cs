using Training.Domain.Enums;

namespace Training.Domain.Entities;

/// <summary>
/// Represents a gymnastics skill or exercise in the global skills catalog.
/// Skills are NOT tenant-scoped (no IMultiTenant) - they are shared across all tenants.
/// CreatedByTenantId and CreatedByUserId track attribution.
/// </summary>
public sealed class Skill
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int EffectivenessRating { get; private set; }
    public string? ImageUrl { get; private set; }
    public int UsageCount { get; private set; }
    public float[]? EmbeddingVector { get; private set; }
    public Guid CreatedByTenantId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastModifiedAt { get; private set; }

    private readonly List<SkillSection> _sections = [];
    public IReadOnlyCollection<SkillSection> Sections => _sections.AsReadOnly();

    private Skill() { } // EF Core

    public static Skill Create(
        string title,
        string description,
        int effectivenessRating,
        Guid createdByTenantId,
        Guid createdByUserId,
        string? imageUrl = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required", nameof(description));

        if (effectivenessRating < 1 || effectivenessRating > 5)
            throw new ArgumentException("Effectiveness rating must be between 1 and 5", nameof(effectivenessRating));

        var now = DateTimeOffset.UtcNow;

        return new Skill
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            EffectivenessRating = effectivenessRating,
            ImageUrl = imageUrl,
            UsageCount = 0,
            CreatedByTenantId = createdByTenantId,
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            LastModifiedAt = now
        };
    }

    public void Update(string title, string description, int effectivenessRating, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required", nameof(description));

        if (effectivenessRating < 1 || effectivenessRating > 5)
            throw new ArgumentException("Effectiveness rating must be between 1 and 5", nameof(effectivenessRating));

        Title = title;
        Description = description;
        EffectivenessRating = effectivenessRating;
        ImageUrl = imageUrl;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void SetEmbedding(float[] embedding)
    {
        if (embedding == null || embedding.Length != 384)
            throw new ArgumentException("Embedding must be exactly 384 dimensions", nameof(embedding));

        EmbeddingVector = embedding;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void IncrementUsageCount()
    {
        UsageCount++;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void DecrementUsageCount()
    {
        if (UsageCount > 0)
        {
            UsageCount--;
            LastModifiedAt = DateTimeOffset.UtcNow;
        }
    }
}
