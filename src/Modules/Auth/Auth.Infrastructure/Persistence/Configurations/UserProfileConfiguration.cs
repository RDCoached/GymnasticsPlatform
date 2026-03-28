using Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Infrastructure.Persistence.Configurations;

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(u => u.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(u => u.KeycloakUserId)
            .HasColumnName("keycloak_user_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(u => u.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.Property(u => u.OnboardingCompleted)
            .HasColumnName("onboarding_completed")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.OnboardingChoice)
            .HasColumnName("onboarding_choice")
            .HasMaxLength(20);

        // Indexes
        builder.HasIndex(u => u.TenantId)
            .HasDatabaseName("ix_user_profiles_tenant_id");

        builder.HasIndex(u => new { u.TenantId, u.KeycloakUserId })
            .IsUnique()
            .HasDatabaseName("ix_user_profiles_tenant_keycloak_user");

        builder.HasIndex(u => new { u.TenantId, u.Email })
            .HasDatabaseName("ix_user_profiles_tenant_email");
    }
}
