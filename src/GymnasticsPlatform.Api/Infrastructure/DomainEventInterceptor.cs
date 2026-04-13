using Common.Core.DomainEvents;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wolverine;

namespace GymnasticsPlatform.Api.Infrastructure;

public sealed class DomainEventInterceptor(IMessageBus messageBus) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return await base.SavedChangesAsync(eventData, result, cancellationToken);

        var entitiesWithEvents = eventData.Context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            foreach (var domainEvent in entity.DomainEvents)
            {
                await messageBus.PublishAsync(domainEvent);
            }
            entity.ClearDomainEvents();
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
