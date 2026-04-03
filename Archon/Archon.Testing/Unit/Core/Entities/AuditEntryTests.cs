using Archon.Core.Entities;
using Archon.Core.ValueObjects;

namespace Archon.Testing.Unit.Core.Entities
{
    public sealed class AuditEntryTests
    {
        [Test]
        public void Constructor_ShouldNormalizeValues_AndSetCreatedAt()
        {
            DateTimeOffset changedAt = DateTimeOffset.UtcNow;

            AuditEntry auditEntry = new AuditEntry(
                "  Customer  ",
                "  10  ",
                AuditAction.Update,
                changedAt,
                changedBy: "15",
                correlationId: "trace-1",
                tenantId: "  tenant-a  ",
                source: "EntityFramework",
                parentEntityName: "  Order  ",
                parentEntityId: "  99  ");

            Assert.That(auditEntry.EntityName, Is.EqualTo("Customer"));
            Assert.That(auditEntry.EntityId, Is.EqualTo("10"));
            Assert.That(auditEntry.TenantId, Is.EqualTo("tenant-a"));
            Assert.That(auditEntry.ParentEntityName, Is.EqualTo("Order"));
            Assert.That(auditEntry.ParentEntityId, Is.EqualTo("99"));
            Assert.That(auditEntry.Action, Is.EqualTo(AuditAction.Update));
            Assert.That(auditEntry.ChangedAt, Is.EqualTo(changedAt));
            Assert.That(auditEntry.CreatedAt, Is.EqualTo(changedAt));
            Assert.That(auditEntry.UpdatedAt, Is.EqualTo(changedAt));
        }

        [Test]
        public void AddPropertyChange_ShouldAddNewPropertyChange()
        {
            AuditEntry auditEntry = new AuditEntry("Customer", "10", AuditAction.Update, DateTimeOffset.UtcNow);

            auditEntry.AddPropertyChange("Name", "Old", "New");

            Assert.That(auditEntry.PropertyChanges.Count, Is.EqualTo(1));
            AuditPropertyChange propertyChange = auditEntry.PropertyChanges.Single();
            Assert.That(propertyChange.PropertyName, Is.EqualTo("Name"));
            Assert.That(propertyChange.OldValue, Is.EqualTo("Old"));
            Assert.That(propertyChange.NewValue, Is.EqualTo("New"));
            Assert.That(propertyChange.AuditEntry, Is.SameAs(auditEntry));
        }

        [Test]
        public void Constructor_ShouldThrow_WhenEntityNameIsEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
                _ = new AuditEntry(" ", "10", AuditAction.Insert, DateTimeOffset.UtcNow));
        }

        [Test]
        public void Constructor_ShouldThrow_WhenEntityIdIsEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
                _ = new AuditEntry("Customer", " ", AuditAction.Insert, DateTimeOffset.UtcNow));
        }
    }
}
