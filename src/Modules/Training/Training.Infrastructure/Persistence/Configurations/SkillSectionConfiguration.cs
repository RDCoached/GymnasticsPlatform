using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Training.Domain.Entities;

namespace Training.Infrastructure.Persistence.Configurations;

internal sealed class SkillSectionConfiguration : IEntityTypeConfiguration<SkillSection>
{
    public void Configure(EntityTypeBuilder<SkillSection> builder)
    {
        builder.ToTable("skill_sections");

        builder.HasKey(ss => ss.Id);

        builder.Property(ss => ss.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(ss => ss.SkillId)
            .HasColumnName("skill_id")
            .IsRequired();

        builder.Property(ss => ss.Section)
            .HasColumnName("section")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(ss => ss.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(ss => ss.SkillId)
            .HasDatabaseName("ix_skill_sections_skill_id");

        builder.HasIndex(ss => ss.Section)
            .HasDatabaseName("ix_skill_sections_section");

        // Unique constraint: one entry per skill-section pair
        builder.HasIndex(ss => new { ss.SkillId, ss.Section })
            .IsUnique()
            .HasDatabaseName("ix_skill_sections_skill_section_unique");
    }
}
