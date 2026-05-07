using Archon.Core.ValueObjects;

namespace Archon.Core.Notifications
{
    public sealed class CreateNotificationRequest
    {
        public long? UserId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public NotificationType Type { get; set; } = NotificationType.Info;

        public string? Link { get; set; }

        public string? Source { get; set; }

        public string? ReferenceEntityName { get; set; }

        public string? ReferenceEntityId { get; set; }
    }
}
