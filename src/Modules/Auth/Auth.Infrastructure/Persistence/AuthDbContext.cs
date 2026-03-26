using Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Common.Core;

namespace Auth.Infrastructure.Persistence;

public sealed class AuthDbContext(
    DbContextOptions<AuthDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);

        // Apply global query filter for multi-tenancy
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IMultiTenant).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var property = System.Linq.Expressions.Expression.Property(parameter, nameof(IMultiTenant.TenantId));

                // Build expression: e.TenantId == tenantContext.TenantId.Value
                var contextParameter = System.Linq.Expressions.Expression.Constant(tenantContext);
                var tenantIdProperty = System.Linq.Expressions.Expression.Property(contextParameter, nameof(ITenantContext.TenantId));
                var tenantIdValue = System.Linq.Expressions.Expression.Property(tenantIdProperty, nameof(Nullable<Guid>.Value));

                var equals = System.Linq.Expressions.Expression.Equal(property, tenantIdValue);
                var lambda = System.Linq.Expressions.Expression.Lambda(equals, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auto-set TenantId on new entities
        foreach (var entry in ChangeTracker.Entries<IMultiTenant>()
            .Where(e => e.State == EntityState.Added))
        {
            if (tenantContext.TenantId is null)
                throw new InvalidOperationException("TenantId is required for creating multi-tenant entities");

            entry.Property(nameof(IMultiTenant.TenantId)).CurrentValue = tenantContext.TenantId.Value;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
