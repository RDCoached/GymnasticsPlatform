using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Training.Domain.Entities;

namespace Training.Infrastructure.Persistence.Configurations;

internal sealed class CoachGymnastRelationshipConfiguration : IEntityTypeConfiguration<CoachGymnastRelationship>
{
    public void Configure(EntityTypeBuilder<CoachGymnastRelationship> builder)
    {
        builder.ToTable("coach_gymnast_relationships");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(r => r.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(r => r.CoachId)
            .HasColumnName("coach_id")
            .IsRequired();

        builder.Property(r => r.GymnastId)
            .HasColumnName("gymnast_id")
            .IsRequired();

        builder.Property(r => r.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(r => r.TenantId)
            .HasDatabaseName("ix_coach_gymnast_relationships_tenant_id");

        builder.HasIndex(r => r.CoachId)
            .HasDatabaseName("ix_coach_gymnast_relationships_coach_id");

        builder.HasIndex(r => r.GymnastId)
            .HasDatabaseName("ix_coach_gymnast_relationships_gymnast_id");

        // Unique constraint: One active relationship per coach-gymnast pair within a tenant
        builder.HasIndex(r => new { r.TenantId, r.CoachId, r.GymnastId })
            .IsUnique()
            .HasDatabaseName("ix_coach_gymnast_relationships_unique");
    }
}
