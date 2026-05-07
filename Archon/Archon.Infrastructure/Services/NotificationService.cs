using Archon.Application.Abstractions;
using Archon.Application.MultiTenancy;
using Archon.Application.Services;
using Archon.Core.Entities;
using Archon.Core.Notifications;
using Archon.Core.Pagination;
using Archon.Infrastructure.Persistence.EF;
using Microsoft.EntityFrameworkCore;

namespace Archon.Infrastructure.Services
{
    public sealed class NotificationService : INotificationService
    {
        private readonly DbContext dbContext;
        private readonly ICurrentUser currentUser;
        private readonly ITenantContext tenantContext;

        public NotificationService(DbContext dbContext, ICurrentUser currentUser, ITenantContext tenantContext)
        {
            this.dbContext = dbContext;
            this.currentUser = currentUser;
            this.tenantContext = tenantContext;
        }

        public async Task<PagedResult<NotificationModel>> GetForCurrentUser(PagedRequest request, bool unreadOnly = false, CancellationToken cancellationToken = default)
        {
            long? userId = currentUser.UserId;
            string? tenantId = tenantContext.TenantId;

            IQueryable<Notification> query = dbContext.Set<Notification>()
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId)
                .Where(item => item.UserId == null || item.UserId == userId);

            if (unreadOnly)
            {
                query = query.Where(item => !item.IsRead);
            }

            return await query
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => Map(item))
                .ToPagedResultAsync(request, cancellationToken);
        }

        public async Task<NotificationModel?> GetById(long id, CancellationToken cancellationToken = default)
        {
            long? userId = currentUser.UserId;
            string? tenantId = tenantContext.TenantId;

            Notification? entity = await dbContext.Set<Notification>()
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.Id == id &&
                    item.TenantId == tenantId &&
                    (item.UserId == null || item.UserId == userId),
                    cancellationToken);

            return entity is null ? null : Map(entity);
        }

        public async Task<int> GetUnreadCountForCurrentUser(CancellationToken cancellationToken = default)
        {
            long? userId = currentUser.UserId;
            string? tenantId = tenantContext.TenantId;

            return await dbContext.Set<Notification>()
                .AsNoTracking()
                .CountAsync(item =>
                    item.TenantId == tenantId &&
                    !item.IsRead &&
                    (item.UserId == null || item.UserId == userId),
                    cancellationToken);
        }

        public async Task<NotificationModel> Create(CreateNotificationRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            Notification notification = new(
                title: request.Title,
                message: request.Message,
                type: request.Type,
                userId: request.UserId,
                tenantId: tenantContext.TenantId,
                link: request.Link,
                source: request.Source,
                referenceEntityName: request.ReferenceEntityName,
                referenceEntityId: request.ReferenceEntityId);

            dbContext.Set<Notification>().Add(notification);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Map(notification);
        }

        public async Task MarkAsRead(long id, CancellationToken cancellationToken = default)
        {
            long? userId = currentUser.UserId;
            string? tenantId = tenantContext.TenantId;

            Notification? entity = await dbContext.Set<Notification>()
                .AsTracking()
                .FirstOrDefaultAsync(item =>
                    item.Id == id &&
                    item.TenantId == tenantId &&
                    (item.UserId == null || item.UserId == userId),
                    cancellationToken);

            if (entity is null)
            {
                return;
            }

            entity.MarkAsRead();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkAllAsReadForCurrentUser(CancellationToken cancellationToken = default)
        {
            long? userId = currentUser.UserId;
            string? tenantId = tenantContext.TenantId;
            DateTimeOffset now = DateTimeOffset.UtcNow;

            await dbContext.Set<Notification>()
                .Where(item =>
                    item.TenantId == tenantId &&
                    !item.IsRead &&
                    (item.UserId == null || item.UserId == userId))
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(item => item.IsRead, true)
                    .SetProperty(item => item.ReadAt, now)
                    .SetProperty(item => item.UpdatedAt, now),
                    cancellationToken);
        }

        public async Task Delete(long id, CancellationToken cancellationToken = default)
        {
            long? userId = currentUser.UserId;
            string? tenantId = tenantContext.TenantId;

            Notification? entity = await dbContext.Set<Notification>()
                .AsTracking()
                .FirstOrDefaultAsync(item =>
                    item.Id == id &&
                    item.TenantId == tenantId &&
                    (item.UserId == null || item.UserId == userId),
                    cancellationToken);

            if (entity is null)
            {
                return;
            }

            dbContext.Set<Notification>().Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task ClearAllForCurrentUser(CancellationToken cancellationToken = default)
        {
            long? userId = currentUser.UserId;
            string? tenantId = tenantContext.TenantId;

            await dbContext.Set<Notification>()
                .Where(item =>
                    item.TenantId == tenantId &&
                    (item.UserId == null || item.UserId == userId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        private static NotificationModel Map(Notification entity) => new()
        {
            Id = entity.Id,
            UserId = entity.UserId,
            TenantId = entity.TenantId,
            Title = entity.Title,
            Message = entity.Message,
            Type = entity.Type,
            IsRead = entity.IsRead,
            ReadAt = entity.ReadAt,
            Link = entity.Link,
            Source = entity.Source,
            ReferenceEntityName = entity.ReferenceEntityName,
            ReferenceEntityId = entity.ReferenceEntityId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
