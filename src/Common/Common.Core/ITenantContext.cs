namespace Common.Core;

/// <summary>
/// Provides the current tenant context for the request
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant ID from the JWT claim
    /// </summary>
    Guid? TenantId { get; }
}
