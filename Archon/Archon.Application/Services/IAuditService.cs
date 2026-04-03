using Archon.Core.Auditing;
using Archon.Core.Pagination;

namespace Archon.Application.Services
{
    public interface IAuditService
    {
        Task<PagedResult<AuditEntryModel>> GetByEntityAsync(string entityName, string entityId, PagedRequest request, CancellationToken cancellationToken = default);

        Task<AuditEntryModel?> GetByIdAsync(long auditEntryId, CancellationToken cancellationToken = default);
    }
}
