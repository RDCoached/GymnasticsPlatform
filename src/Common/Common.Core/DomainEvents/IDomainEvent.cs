namespace Common.Core.DomainEvents;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
