using Archon.Core.ValueObjects;

namespace Archon.Core.Entities
{
    public class Notification : Entity
    {
        public long? UserId { get; private set; }

        public string? TenantId { get; private set; }

        public string Title { get; private set; } = string.Empty;

        public string Message { get; private set; } = string.Empty;

        public NotificationType Type { get; private set; }

        public bool IsRead { get; private set; }

        public DateTimeOffset? ReadAt { get; private set; }

        public string? Link { get; private set; }

        public string? Source { get; private set; }

        public string? ReferenceEntityName { get; private set; }

        public string? ReferenceEntityId { get; private set; }

        private Notification()
        {
        }

        public Notification(
            string title,
            string message,
            NotificationType type = NotificationType.Info,
            long? userId = null,
            string? tenantId = null,
            string? link = null,
            string? source = null,
            string? referenceEntityName = null,
            string? referenceEntityId = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            Title = title.Trim();
            Message = message.Trim();
            Type = type;
            UserId = userId;
            TenantId = Normalize(tenantId);
            Link = Normalize(link);
            Source = Normalize(source);
            ReferenceEntityName = Normalize(referenceEntityName);
            ReferenceEntityId = Normalize(referenceEntityId);
            IsRead = false;
            SetCreatedAt(DateTimeOffset.UtcNow);
        }

        public void MarkAsRead()
        {
            if (IsRead)
            {
                return;
            }

            IsRead = true;
            ReadAt = DateTimeOffset.UtcNow;
            SetUpdatedAt(ReadAt.Value);
        }

        public void MarkAsUnread()
        {
            if (!IsRead)
            {
                return;
            }

            IsRead = false;
            ReadAt = null;
            SetUpdatedAt(DateTimeOffset.UtcNow);
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
