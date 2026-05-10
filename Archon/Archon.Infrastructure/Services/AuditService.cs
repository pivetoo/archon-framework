using Archon.Application.Services;
using Archon.Core.Auditing;
using Archon.Core.Entities;
using Archon.Core.Pagination;
using Archon.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Archon.Infrastructure.Services
{
    public sealed class AuditService : IAuditService
    {
        private readonly DbContext dbContext;

        public AuditService(DbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public Task<PagedResult<AuditEntryModel>> GetByEntity(string entityName, string entityId, PagedRequest request, CancellationToken cancellationToken = default)
        {
            IQueryable<AuditEntry> query = dbContext.Set<AuditEntry>()
                .AsNoTracking()
                .Where(entry =>
                    (entry.EntityName == entityName && entry.EntityId == entityId) ||
                    (entry.ParentEntityName == entityName && entry.ParentEntityId == entityId))
                .OrderByDescending(entry => entry.ChangedAt);

            return ToPagedAuditResultAsync(query, request, includePropertyChanges: false, cancellationToken);
        }

        public async Task<AuditEntryModel?> GetById(long auditEntryId, CancellationToken cancellationToken = default)
        {
            AuditEntry? entry = await dbContext.Set<AuditEntry>()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == auditEntryId, cancellationToken);

            if (entry is null)
            {
                return null;
            }

            Dictionary<long, IReadOnlyCollection<AuditPropertyChangeModel>> propertyChangesByAuditEntryId = await LoadPropertyChangesAsync([entry.Id], cancellationToken);
            return ToModel(entry, propertyChangesByAuditEntryId);
        }

        public Task<PagedResult<AuditEntryModel>> Search(
            string? entityName,
            AuditAction? action,
            string? changedBy,
            DateTimeOffset? from,
            DateTimeOffset? to,
            PagedRequest request,
            CancellationToken cancellationToken = default)
        {
            IQueryable<AuditEntry> query = ApplyFilters(dbContext.Set<AuditEntry>().AsNoTracking(), entityName, action, changedBy, from, to)
                .OrderByDescending(entry => entry.ChangedAt);

            return ToPagedAuditResultAsync(query, request, includePropertyChanges: false, cancellationToken);
        }

        public async Task<AuditStatsModel> GetStats(
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken cancellationToken = default)
        {
            IQueryable<AuditEntry> query = ApplyFilters(dbContext.Set<AuditEntry>().AsNoTracking(), entityName: null, action: null, changedBy: null, from, to);

            long totalEntries = await query.LongCountAsync(cancellationToken);

            var volumeRaw = await query
                .GroupBy(entry => new { entry.ChangedAt.Year, entry.ChangedAt.Month, entry.ChangedAt.Day })
                .Select(group => new { group.Key.Year, group.Key.Month, group.Key.Day, Count = group.LongCount() })
                .ToListAsync(cancellationToken);

            List<AuditVolumePoint> volumeByDay = volumeRaw
                .Select(item => new AuditVolumePoint
                {
                    Date = new DateOnly(item.Year, item.Month, item.Day),
                    Count = item.Count,
                })
                .OrderBy(point => point.Date)
                .ToList();

            List<AuditCountByName> topUsers = await query
                .Where(entry => entry.ChangedBy != null && entry.ChangedBy != string.Empty)
                .GroupBy(entry => entry.ChangedBy!)
                .Select(group => new AuditCountByName { Name = group.Key, Count = group.LongCount() })
                .OrderByDescending(item => item.Count)
                .Take(10)
                .ToListAsync(cancellationToken);

            List<AuditCountByName> topEntities = await query
                .GroupBy(entry => entry.EntityName)
                .Select(group => new AuditCountByName { Name = group.Key, Count = group.LongCount() })
                .OrderByDescending(item => item.Count)
                .Take(10)
                .ToListAsync(cancellationToken);

            List<AuditActionCount> actionDistribution = await query
                .GroupBy(entry => entry.Action)
                .Select(group => new AuditActionCount { Action = group.Key, Count = group.LongCount() })
                .ToListAsync(cancellationToken);

            return new AuditStatsModel
            {
                TotalEntries = totalEntries,
                VolumeByDay = volumeByDay,
                TopUsers = topUsers,
                TopEntities = topEntities,
                ActionDistribution = actionDistribution,
            };
        }

        private static IQueryable<AuditEntry> ApplyFilters(
            IQueryable<AuditEntry> query,
            string? entityName,
            AuditAction? action,
            string? changedBy,
            DateTimeOffset? from,
            DateTimeOffset? to)
        {
            if (!string.IsNullOrWhiteSpace(entityName))
            {
                string normalized = entityName.Trim();
                query = query.Where(entry => entry.EntityName == normalized);
            }

            if (action.HasValue)
            {
                AuditAction value = action.Value;
                query = query.Where(entry => entry.Action == value);
            }

            if (!string.IsNullOrWhiteSpace(changedBy))
            {
                string normalized = changedBy.Trim();
                query = query.Where(entry => entry.ChangedBy != null && entry.ChangedBy == normalized);
            }

            if (from.HasValue)
            {
                DateTimeOffset value = from.Value;
                query = query.Where(entry => entry.ChangedAt >= value);
            }

            if (to.HasValue)
            {
                DateTimeOffset value = to.Value;
                query = query.Where(entry => entry.ChangedAt <= value);
            }

            return query;
        }

        private async Task<PagedResult<AuditEntryModel>> ToPagedAuditResultAsync(IQueryable<AuditEntry> query, PagedRequest request, bool includePropertyChanges, CancellationToken cancellationToken)
        {
            long totalCount = await query.LongCountAsync(cancellationToken);
            List<AuditEntry> entries = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            Dictionary<long, IReadOnlyCollection<AuditPropertyChangeModel>> propertyChangesByAuditEntryId = includePropertyChanges
                ? await LoadPropertyChangesAsync(entries.Select(entry => entry.Id).ToList(), cancellationToken)
                : [];
            int totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)request.PageSize);

            return new PagedResult<AuditEntryModel>
            {
                Items = entries
                    .Select(entry => ToModel(entry, propertyChangesByAuditEntryId))
                    .ToList(),
                Pagination = new PaginationMetadata
                {
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalCount = totalCount,
                    TotalPages = totalPages
                }
            };
        }

        private async Task<Dictionary<long, IReadOnlyCollection<AuditPropertyChangeModel>>> LoadPropertyChangesAsync(IReadOnlyCollection<long> auditEntryIds, CancellationToken cancellationToken)
        {
            if (auditEntryIds.Count == 0)
            {
                return [];
            }

            List<AuditPropertyChange> propertyChanges = await dbContext.Set<AuditPropertyChange>()
                .AsNoTracking()
                .Where(change => auditEntryIds.Contains(change.AuditEntryId))
                .OrderBy(change => change.Id)
                .ToListAsync(cancellationToken);

            return propertyChanges
                .GroupBy(change => change.AuditEntryId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyCollection<AuditPropertyChangeModel>)group
                        .Select(change => new AuditPropertyChangeModel
                        {
                            PropertyName = change.PropertyName,
                            OldValue = change.OldValue,
                            NewValue = change.NewValue
                        })
                        .ToList());
        }

        private static AuditEntryModel ToModel(AuditEntry entry, IReadOnlyDictionary<long, IReadOnlyCollection<AuditPropertyChangeModel>> propertyChangesByAuditEntryId)
        {
            propertyChangesByAuditEntryId.TryGetValue(entry.Id, out IReadOnlyCollection<AuditPropertyChangeModel>? propertyChanges);

            return new AuditEntryModel
            {
                Id = entry.Id,
                EntityName = entry.EntityName,
                EntityId = entry.EntityId,
                TenantId = entry.TenantId,
                Action = entry.Action,
                ChangedAt = entry.ChangedAt,
                ChangedBy = entry.ChangedBy,
                CorrelationId = entry.CorrelationId,
                ParentEntityName = entry.ParentEntityName,
                ParentEntityId = entry.ParentEntityId,
                Source = entry.Source,
                PropertyChanges = propertyChanges ?? Array.Empty<AuditPropertyChangeModel>()
            };
        }
    }
}
