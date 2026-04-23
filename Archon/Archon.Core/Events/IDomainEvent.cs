namespace Archon.Core.Events
{
    public interface IDomainEvent
    {
        DateTimeOffset OccurredAt { get; }
    }
}
