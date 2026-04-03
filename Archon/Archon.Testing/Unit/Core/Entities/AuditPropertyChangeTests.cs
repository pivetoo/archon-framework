using Archon.Core.Entities;
using Archon.Core.ValueObjects;

namespace Archon.Testing.Unit.Core.Entities
{
    public sealed class AuditPropertyChangeTests
    {
        [Test]
        public void Constructor_ShouldSetCreatedAt_FromAuditEntryChangedAt()
        {
            DateTimeOffset changedAt = DateTimeOffset.UtcNow;
            AuditEntry auditEntry = new AuditEntry("Customer", "10", AuditAction.Update, changedAt);

            AuditPropertyChange propertyChange = new AuditPropertyChange(auditEntry, "Name", "Old", "New");

            Assert.That(propertyChange.CreatedAt, Is.EqualTo(changedAt));
            Assert.That(propertyChange.PropertyName, Is.EqualTo("Name"));
            Assert.That(propertyChange.OldValue, Is.EqualTo("Old"));
            Assert.That(propertyChange.NewValue, Is.EqualTo("New"));
        }

        [Test]
        public void Constructor_ShouldThrow_WhenAuditEntryIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _ = new AuditPropertyChange(null!, "Name", "Old", "New"));
        }

        [Test]
        public void Constructor_ShouldThrow_WhenPropertyNameIsEmpty()
        {
            AuditEntry auditEntry = new AuditEntry("Customer", "10", AuditAction.Update, DateTimeOffset.UtcNow);

            Assert.Throws<ArgumentException>(() =>
                _ = new AuditPropertyChange(auditEntry, " ", "Old", "New"));
        }
    }
}
