using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Training.Domain.Entities;

namespace Training.Infrastructure.Persistence.Configurations;

internal sealed class ProgrammeMetadataConfiguration : IEntityTypeConfiguration<ProgrammeMetadata>
{
    public void Configure(EntityTypeBuilder<ProgrammeMetadata> builder)
    {
        builder.ToTable("programme_metadata");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(p => p.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(p => p.GymnastId)
            .HasColumnName("gymnast_id")
            .IsRequired();

        builder.Property(p => p.CoachId)
            .HasColumnName("coach_id")
            .IsRequired();

        builder.Property(p => p.CouchDbDocId)
            .HasColumnName("couchdb_doc_id")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.CouchDbRev)
            .HasColumnName("couchdb_rev")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.Title)
            .HasColumnName("title")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(p => p.StartDate)
            .HasColumnName("start_date")
            .IsRequired();

        builder.Property(p => p.EndDate)
            .HasColumnName("end_date")
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.LastModifiedAt)
            .HasColumnName("last_modified_at")
            .IsRequired();

        builder.Property(p => p.EmbeddingVector)
            .HasColumnName("embedding_vector")
            .HasColumnType("vector")
            .HasConversion(
                v => v != null ? new Vector(v) : null,
                v => v != null ? v.ToArray() : null);

        // Indexes
        builder.HasIndex(p => p.TenantId)
            .HasDatabaseName("ix_programme_metadata_tenant_id");

        builder.HasIndex(p => p.GymnastId)
            .HasDatabaseName("ix_programme_metadata_gymnast_id");

        builder.HasIndex(p => p.CoachId)
            .HasDatabaseName("ix_programme_metadata_coach_id");

        builder.HasIndex(p => p.CouchDbDocId)
            .IsUnique()
            .HasDatabaseName("ix_programme_metadata_couchdb_doc_id");

        builder.HasIndex(p => new { p.TenantId, p.GymnastId, p.Status })
            .HasDatabaseName("ix_programme_metadata_tenant_gymnast_status");

        // Business rule: Only one Active programme per gymnast
        builder.HasIndex(p => new { p.TenantId, p.GymnastId })
            .IsUnique()
            .HasFilter("status = 1")
            .HasDatabaseName("ix_programme_metadata_active_per_gymnast");
    }
}
