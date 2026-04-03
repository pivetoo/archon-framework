using Archon.Core.Auditing;
using Archon.Core.Pagination;

namespace Archon.Application.Services
{
    public interface IAuditService
    {
        Task<PagedResult<AuditEntryModel>> GetByEntity(string entityName, string entityId, PagedRequest request, CancellationToken cancellationToken = default);

        Task<AuditEntryModel?> GetById(long auditEntryId, CancellationToken cancellationToken = default);
    }
}
