namespace Common.Core;

/// <summary>
/// Marker interface for entities that belong to a specific tenant
/// </summary>
public interface IMultiTenant
{
    Guid TenantId { get; }
}
