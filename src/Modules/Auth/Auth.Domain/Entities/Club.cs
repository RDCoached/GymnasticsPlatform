using Common.Core;

namespace Auth.Domain.Entities;

public sealed class Club : IMultiTenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private Club() { }

    public static Club Create(string name, string ownerUserId, TimeProvider clock)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Club name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException("Owner user ID cannot be empty.", nameof(ownerUserId));

        return new Club
        {
            Id = Guid.NewGuid(),
            Name = name,
            TenantId = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            CreatedAt = clock.GetUtcNow()
        };
    }
}
