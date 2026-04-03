namespace Archon.Core.Entities
{
    public class AuditPropertyChange : Entity
    {
        public long AuditEntryId { get; private set; }

        public AuditEntry AuditEntry { get; private set; } = null!;

        public string PropertyName { get; private set; } = string.Empty;

        public string? OldValue { get; private set; }

        public string? NewValue { get; private set; }

        public AuditPropertyChange(AuditEntry auditEntry, string propertyName, string? oldValue, string? newValue)
        {
            if (auditEntry is null)
            {
                throw new ArgumentNullException(nameof(auditEntry));
            }

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("Property name cannot be empty.", nameof(propertyName));
            }

            AuditEntry = auditEntry;
            PropertyName = propertyName.Trim();
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
