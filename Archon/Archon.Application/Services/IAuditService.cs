using Archon.Core.Auditing;
using Archon.Core.Pagination;
using Archon.Core.ValueObjects;

namespace Archon.Application.Services
{
    public interface IAuditService
    {
        Task<PagedResult<AuditEntryModel>> GetByEntity(string entityName, string entityId, PagedRequest request, CancellationToken cancellationToken = default);

        Task<AuditEntryModel?> GetById(long auditEntryId, CancellationToken cancellationToken = default);

        Task<PagedResult<AuditEntryModel>> Search(
            string? entityName,
            AuditAction? action,
            string? changedBy,
            DateTimeOffset? from,
            DateTimeOffset? to,
            PagedRequest request,
            CancellationToken cancellationToken = default);

        Task<AuditStatsModel> GetStats(
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken cancellationToken = default);
    }
}
