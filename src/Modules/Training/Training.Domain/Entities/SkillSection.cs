using Training.Domain.Enums;

namespace Training.Domain.Entities;

/// <summary>
/// Junction entity for the many-to-many relationship between Skills and GymnasticSections.
/// Does NOT implement IMultiTenant - follows the global nature of Skills.
/// </summary>
public sealed class SkillSection
{
    public Guid Id { get; private set; }
    public Guid SkillId { get; private set; }
    public GymnasticSection Section { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Navigation property
    public Skill Skill { get; private set; } = null!;

    private SkillSection() { } // EF Core

    public static SkillSection Create(Guid skillId, GymnasticSection section)
    {
        if (skillId == Guid.Empty)
            throw new ArgumentException("Skill ID is required", nameof(skillId));

        return new SkillSection
        {
            Id = Guid.NewGuid(),
            SkillId = skillId,
            Section = section,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
