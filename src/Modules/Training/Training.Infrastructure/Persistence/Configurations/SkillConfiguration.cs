using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Training.Domain.Entities;

namespace Training.Infrastructure.Persistence.Configurations;

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(s => s.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasColumnName("description")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(s => s.EffectivenessRating)
            .HasColumnName("effectiveness_rating")
            .IsRequired();

        builder.Property(s => s.ImageUrl)
            .HasColumnName("image_url")
            .HasMaxLength(500);

        builder.Property(s => s.UsageCount)
            .HasColumnName("usage_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(s => s.EmbeddingVector)
            .HasColumnName("embedding_vector")
            .HasColumnType("vector")
            .HasConversion(
                v => v != null ? new Vector(v) : null,
                v => v != null ? v.ToArray() : null);

        builder.Property(s => s.CreatedByTenantId)
            .HasColumnName("created_by_tenant_id")
            .IsRequired();

        builder.Property(s => s.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.LastModifiedAt)
            .HasColumnName("last_modified_at")
            .IsRequired();

        // Navigation: Skill -> SkillSections (one-to-many)
        builder.HasMany(s => s.Sections)
            .WithOne(ss => ss.Skill)
            .HasForeignKey(ss => ss.SkillId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(s => s.Title)
            .HasDatabaseName("ix_skills_title");

        builder.HasIndex(s => s.EffectivenessRating)
            .HasDatabaseName("ix_skills_effectiveness_rating");

        builder.HasIndex(s => s.UsageCount)
            .HasDatabaseName("ix_skills_usage_count");

        builder.HasIndex(s => s.CreatedByTenantId)
            .HasDatabaseName("ix_skills_created_by_tenant_id");
    }
}
