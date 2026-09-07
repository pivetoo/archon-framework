namespace Archon.Core.Access
{
    public sealed class AccessResourceSyncResult
    {
        public int CreatedCount { get; init; }

        public int UpdatedCount { get; init; }

        public int DeactivatedCount { get; init; }

        public int TotalCount { get; init; }
    }
}
