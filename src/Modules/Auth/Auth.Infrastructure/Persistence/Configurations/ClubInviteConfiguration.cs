using Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Infrastructure.Persistence.Configurations;

internal sealed class ClubInviteConfiguration : IEntityTypeConfiguration<ClubInvite>
{
    public void Configure(EntityTypeBuilder<ClubInvite> builder)
    {
        builder.ToTable("club_invites");

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(ci => ci.ClubId)
            .HasColumnName("club_id")
            .IsRequired();

        builder.Property(ci => ci.Code)
            .HasColumnName("code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(ci => ci.MaxUses)
            .HasColumnName("max_uses")
            .IsRequired();

        builder.Property(ci => ci.TimesUsed)
            .HasColumnName("times_used")
            .IsRequired();

        builder.Property(ci => ci.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(ci => ci.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Foreign key relationship
        builder.HasOne<Club>()
            .WithMany()
            .HasForeignKey(ci => ci.ClubId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(ci => ci.Code)
            .IsUnique()
            .HasDatabaseName("ix_club_invites_code");

        builder.HasIndex(ci => ci.ClubId)
            .HasDatabaseName("ix_club_invites_club_id");

        builder.HasIndex(ci => ci.ExpiresAt)
            .HasDatabaseName("ix_club_invites_expires_at");
    }
}
