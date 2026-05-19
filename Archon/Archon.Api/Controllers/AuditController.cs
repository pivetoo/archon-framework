using Archon.Api.Attributes;
using Archon.Application.Services;
using Archon.Core.Pagination;
using Archon.Core.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Archon.Api.Controllers
{
    [AccessArea("audit.area")]
    public sealed class AuditController : ApiControllerBase
    {
        private readonly IAuditService auditService;

        public AuditController(IAuditService auditService)
        {
            this.auditService = auditService;
        }

        [RequireAccess("audit.getByEntity.description")]
        [GetEndpoint("entity/{entityName}/{entityId}")]
        public async Task<IActionResult> GetByEntity(string entityName, string entityId, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                return Http400(Localizer["request.entity.name.required"]);
            }

            if (string.IsNullOrWhiteSpace(entityId))
            {
                return Http400(Localizer["request.entity.id.required"]);
            }

            var result = await auditService.GetByEntity(entityName, entityId, request, cancellationToken);
            return Http200(result);
        }

        [RequireAccess("audit.getById.description")]
        [GetEndpoint("{auditEntryId:long}")]
        public async Task<IActionResult> GetById(long auditEntryId, CancellationToken cancellationToken)
        {
            if (auditEntryId <= 0)
            {
                return Http400(Localizer["request.auditEntry.id.required"]);
            }

            var result = await auditService.GetById(auditEntryId, cancellationToken);
            return result is null ? Http404(Localizer["record.auditEntry.notFound"]) : Http200(result);
        }

        [RequireAccess("audit.recent.description")]
        [GetEndpoint]
        public async Task<IActionResult> Recent(
            [FromQuery] string? entityName,
            [FromQuery] AuditAction? action,
            [FromQuery] string? changedBy,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] PagedRequest request,
            CancellationToken cancellationToken)
        {
            var result = await auditService.Search(entityName, action, changedBy, from, to, request, cancellationToken);
            return Http200(result);
        }

        [RequireAccess("audit.stats.description")]
        [GetEndpoint]
        public async Task<IActionResult> Stats(
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            CancellationToken cancellationToken)
        {
            var result = await auditService.GetStats(from, to, cancellationToken);
            return Http200(result);
        }
    }
}
