using Archon.Core.ValueObjects;

namespace Archon.Core.Auditing
{
    public sealed class AuditEntryModel
    {
        public long Id { get; init; }

        public string EntityName { get; init; } = string.Empty;

        public string EntityId { get; init; } = string.Empty;

        public string? TenantId { get; init; }

        public AuditAction Action { get; init; }

        public DateTimeOffset ChangedAt { get; init; }

        public string? ChangedBy { get; init; }

        public string? CorrelationId { get; init; }

        public string? ParentEntityName { get; init; }

        public string? ParentEntityId { get; init; }

        public string? Source { get; init; }

        public IReadOnlyCollection<AuditPropertyChangeModel> PropertyChanges { get; init; } = Array.Empty<AuditPropertyChangeModel>();
    }
}
