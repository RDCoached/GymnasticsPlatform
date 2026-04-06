using Common.Core;

namespace Training.Domain.Entities;

/// <summary>
/// Many-to-many relationship between coaches and gymnasts.
/// Multiple coaches can work with the same gymnast and vice versa.
/// </summary>
public sealed class CoachGymnastRelationship : IMultiTenant
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CoachId { get; private set; }
    public Guid GymnastId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private CoachGymnastRelationship() { } // EF Core

    public static CoachGymnastRelationship Create(
        Guid tenantId,
        Guid coachId,
        Guid gymnastId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required", nameof(tenantId));

        if (coachId == Guid.Empty)
            throw new ArgumentException("Coach ID is required", nameof(coachId));

        if (gymnastId == Guid.Empty)
            throw new ArgumentException("Gymnast ID is required", nameof(gymnastId));

        return new CoachGymnastRelationship
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CoachId = coachId,
            GymnastId = gymnastId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate()
    {
        IsActive = true;
    }
}
