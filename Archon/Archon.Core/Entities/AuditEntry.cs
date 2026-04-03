using Archon.Core.ValueObjects;

namespace Archon.Core.Entities;

public class AuditEntry : Entity
{
    public string EntityName { get; private set; } = string.Empty;

    public string EntityId { get; private set; } = string.Empty;

    public AuditAction Action { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public string? ChangedBy { get; private set; }

    public string? CorrelationId { get; private set; }

    public string? Source { get; private set; }

    public IReadOnlyCollection<AuditPropertyChange> PropertyChanges => propertyChanges.AsReadOnly();

    private readonly List<AuditPropertyChange> propertyChanges = [];

    public AuditEntry(
        string entityName,
        string entityId,
        AuditAction action,
        DateTimeOffset changedAt,
        string? changedBy = null,
        string? correlationId = null,
        string? source = null)
    {
        SetEntity(entityName, entityId);
        Action = action;
        ChangedAt = changedAt;
        ChangedBy = changedBy;
        CorrelationId = correlationId;
        Source = source;
    }

    public void AddPropertyChange(string propertyName, string? oldValue, string? newValue)
    {
        propertyChanges.Add(new AuditPropertyChange(this, propertyName, oldValue, newValue));
    }

    private void SetEntity(string entityName, string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException("Entity name cannot be empty.", nameof(entityName));
        }

        if (string.IsNullOrWhiteSpace(entityId))
        {
            throw new ArgumentException("Entity id cannot be empty.", nameof(entityId));
        }

        EntityName = entityName.Trim();
        EntityId = entityId.Trim();
    }
}
