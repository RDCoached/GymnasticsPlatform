using Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Infrastructure.Persistence.Configurations;

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        builder.HasKey(ur => ur.Id);

        builder.Property(ur => ur.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(ur => ur.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(ur => ur.KeycloakUserId)
            .HasColumnName("keycloak_user_id")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(ur => ur.Role)
            .HasColumnName("role")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(ur => ur.AssignedAt)
            .HasColumnName("assigned_at")
            .IsRequired();

        builder.Property(ur => ur.AssignedBy)
            .HasColumnName("assigned_by")
            .HasMaxLength(255);

        // Unique composite index to prevent duplicate role assignments
        builder.HasIndex(ur => new { ur.TenantId, ur.KeycloakUserId, ur.Role })
            .IsUnique()
            .HasDatabaseName("ix_user_roles_tenant_user_role");

        // Index for lookups by tenant and user
        builder.HasIndex(ur => new { ur.TenantId, ur.KeycloakUserId })
            .HasDatabaseName("ix_user_roles_tenant_user");
    }
}
