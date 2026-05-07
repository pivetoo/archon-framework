using Archon.Core.Notifications;
using Archon.Core.Pagination;

namespace Archon.Application.Services
{
    public interface INotificationService
    {
        Task<PagedResult<NotificationModel>> GetForCurrentUser(PagedRequest request, bool unreadOnly = false, CancellationToken cancellationToken = default);

        Task<NotificationModel?> GetById(long id, CancellationToken cancellationToken = default);

        Task<int> GetUnreadCountForCurrentUser(CancellationToken cancellationToken = default);

        Task<NotificationModel> Create(CreateNotificationRequest request, CancellationToken cancellationToken = default);

        Task MarkAsRead(long id, CancellationToken cancellationToken = default);

        Task MarkAllAsReadForCurrentUser(CancellationToken cancellationToken = default);

        Task Delete(long id, CancellationToken cancellationToken = default);

        Task ClearAllForCurrentUser(CancellationToken cancellationToken = default);
    }
}
