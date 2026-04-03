namespace Archon.Core.Auditing
{
    public sealed class AuditPropertyChangeModel
    {
        public string PropertyName { get; init; } = string.Empty;

        public string? OldValue { get; init; }

        public string? NewValue { get; init; }
    }
}
