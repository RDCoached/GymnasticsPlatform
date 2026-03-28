using Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Common.Core;

namespace Auth.Infrastructure.Persistence;

public sealed class AuthDbContext(
    DbContextOptions<AuthDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<ClubInvite> ClubInvites => Set<ClubInvite>();

    // Expose tenant ID as a property for query filter evaluation
    private Guid CurrentTenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId is required");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);

        // Apply global query filter for multi-tenancy
        // The filter references the DbContext instance (this), so it evaluates dynamically per instance
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IMultiTenant).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var property = System.Linq.Expressions.Expression.Property(parameter, nameof(IMultiTenant.TenantId));

                // Build expression: e.TenantId == this.CurrentTenantId
                // Capture 'this' so the tenant ID is evaluated per DbContext instance
                var dbContextParameter = System.Linq.Expressions.Expression.Constant(this);
                var currentTenantIdProperty = System.Linq.Expressions.Expression.Property(dbContextParameter, nameof(CurrentTenantId));

                var equals = System.Linq.Expressions.Expression.Equal(property, currentTenantIdProperty);
                var lambda = System.Linq.Expressions.Expression.Lambda(equals, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auto-set TenantId on new entities that don't already have one
        foreach (var entry in ChangeTracker.Entries<IMultiTenant>()
            .Where(e => e.State == EntityState.Added))
        {
            var currentTenantId = (Guid)entry.Property(nameof(IMultiTenant.TenantId)).CurrentValue!;

            // Only set TenantId if not already set (Guid.Empty)
            if (currentTenantId == Guid.Empty)
            {
                if (_tenantContext.TenantId is null)
                    throw new InvalidOperationException("TenantId is required for creating multi-tenant entities");

                entry.Property(nameof(IMultiTenant.TenantId)).CurrentValue = _tenantContext.TenantId.Value;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
