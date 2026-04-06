using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Training.Domain.Entities;

namespace Training.Infrastructure.Persistence.Configurations;

internal sealed class ProgrammeBuilderSessionConfiguration : IEntityTypeConfiguration<ProgrammeBuilderSession>
{
    public void Configure(EntityTypeBuilder<ProgrammeBuilderSession> builder)
    {
        builder.ToTable("programme_builder_sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(s => s.CoachId)
            .HasColumnName("coach_id")
            .IsRequired();

        builder.Property(s => s.GymnastId)
            .HasColumnName("gymnast_id")
            .IsRequired();

        builder.Property(s => s.InitialGoals)
            .HasColumnName("initial_goals")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(s => s.ConversationHistoryJson)
            .HasColumnName("conversation_history_json")
            .HasColumnType("jsonb");

        builder.Property(s => s.ResultingProgrammeId)
            .HasColumnName("resulting_programme_id");

        builder.Property(s => s.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(s => s.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(s => s.RagScopeConfig)
            .HasColumnName("rag_scope_config")
            .HasMaxLength(1000)
            .IsRequired();

        // Indexes
        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("ix_programme_builder_sessions_tenant_id");

        builder.HasIndex(s => s.CoachId)
            .HasDatabaseName("ix_programme_builder_sessions_coach_id");

        builder.HasIndex(s => s.GymnastId)
            .HasDatabaseName("ix_programme_builder_sessions_gymnast_id");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("ix_programme_builder_sessions_status");

        builder.HasIndex(s => s.ResultingProgrammeId)
            .HasDatabaseName("ix_programme_builder_sessions_resulting_programme_id");
    }
}
