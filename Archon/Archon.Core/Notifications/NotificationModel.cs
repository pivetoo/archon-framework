using Archon.Core.ValueObjects;

namespace Archon.Core.Notifications
{
    public sealed class NotificationModel
    {
        public long Id { get; init; }

        public long? UserId { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;

        public NotificationType Type { get; init; }

        public bool IsRead { get; init; }

        public DateTimeOffset? ReadAt { get; init; }

        public string? Link { get; init; }

        public string? Source { get; init; }

        public string? ReferenceEntityName { get; init; }

        public string? ReferenceEntityId { get; init; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset? UpdatedAt { get; init; }
    }
}
