using Common.Core;
using Microsoft.EntityFrameworkCore;
using Training.Domain.Entities;

namespace Training.Infrastructure.Persistence;

public sealed class TrainingDbContext(
    DbContextOptions<TrainingDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<ProgrammeMetadata> ProgrammeMetadata => Set<ProgrammeMetadata>();
    public DbSet<CoachGymnastRelationship> CoachGymnastRelationships => Set<CoachGymnastRelationship>();
    public DbSet<ProgrammeBuilderSession> ProgrammeBuilderSessions => Set<ProgrammeBuilderSession>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<SkillSection> SkillSections => Set<SkillSection>();

    private Guid CurrentTenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId is required");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrainingDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IMultiTenant).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var property = System.Linq.Expressions.Expression.Property(parameter, nameof(IMultiTenant.TenantId));

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
        foreach (var entry in ChangeTracker.Entries<IMultiTenant>()
            .Where(e => e.State == EntityState.Added))
        {
            var currentTenantId = (Guid)entry.Property(nameof(IMultiTenant.TenantId)).CurrentValue!;

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
