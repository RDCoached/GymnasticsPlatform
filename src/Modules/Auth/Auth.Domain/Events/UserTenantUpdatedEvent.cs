using Common.Core.DomainEvents;

namespace Auth.Domain.Events;

public sealed record UserTenantUpdatedEvent(
    Guid UserId,
    string ProviderUserId,
    Guid OldTenantId,
    Guid NewTenantId,
    DateTimeOffset OccurredAt,
    string Email,
    string FullName) : IDomainEvent;
