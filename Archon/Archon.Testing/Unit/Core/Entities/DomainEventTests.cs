using Archon.Core.Entities;
using Archon.Core.Events;

namespace Archon.Testing.Unit.Core.Entities
{
    public sealed class DomainEventTests
    {
        [Test]
        public void AddDomainEvent_ShouldStoreEvent()
        {
            TestEntity entity = new TestEntity();
            TestDomainEvent domainEvent = new TestDomainEvent();

            entity.AddDomainEvent(domainEvent);

            Assert.That(entity.DomainEvents.Count, Is.EqualTo(1));
            Assert.That(entity.DomainEvents.First(), Is.SameAs(domainEvent));
        }

        [Test]
        public void AddDomainEvent_ShouldThrow_WhenEventIsNull()
        {
            TestEntity entity = new TestEntity();

            Assert.Throws<ArgumentNullException>(() => entity.AddDomainEvent(null!));
        }

        [Test]
        public void RemoveDomainEvent_ShouldRemoveStoredEvent()
        {
            TestEntity entity = new TestEntity();
            TestDomainEvent domainEvent = new TestDomainEvent();
            entity.AddDomainEvent(domainEvent);

            entity.RemoveDomainEvent(domainEvent);

            Assert.That(entity.DomainEvents.Count, Is.EqualTo(0));
        }

        [Test]
        public void ClearDomainEvents_ShouldRemoveAllEvents()
        {
            TestEntity entity = new TestEntity();
            entity.AddDomainEvent(new TestDomainEvent());
            entity.AddDomainEvent(new TestDomainEvent());

            entity.ClearDomainEvents();

            Assert.That(entity.DomainEvents.Count, Is.EqualTo(0));
        }

        private sealed class TestEntity : Entity
        {
        }

        private sealed class TestDomainEvent : IDomainEvent
        {
            public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
        }
    }
}
