using Archon.Core.ValueObjects;

namespace Archon.Core.Auditing
{
    public sealed class AuditStatsModel
    {
        public long TotalEntries { get; init; }

        public IReadOnlyCollection<AuditVolumePoint> VolumeByDay { get; init; } = Array.Empty<AuditVolumePoint>();

        public IReadOnlyCollection<AuditCountByName> TopUsers { get; init; } = Array.Empty<AuditCountByName>();

        public IReadOnlyCollection<AuditCountByName> TopEntities { get; init; } = Array.Empty<AuditCountByName>();

        public IReadOnlyCollection<AuditActionCount> ActionDistribution { get; init; } = Array.Empty<AuditActionCount>();
    }

    public sealed class AuditVolumePoint
    {
        public DateOnly Date { get; init; }

        public long Count { get; init; }
    }

    public sealed class AuditCountByName
    {
        public string Name { get; init; } = string.Empty;

        public long Count { get; init; }
    }

    public sealed class AuditActionCount
    {
        public AuditAction Action { get; init; }

        public long Count { get; init; }
    }
}
