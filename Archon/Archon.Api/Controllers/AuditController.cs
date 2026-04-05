using Archon.Api.Attributes;
using Archon.Application.Services;
using Archon.Core.Pagination;
using Microsoft.AspNetCore.Mvc;

namespace Archon.Api.Controllers
{
    public sealed class AuditController : ApiControllerBase
    {
        private readonly IAuditService auditService;

        public AuditController(IAuditService auditService)
        {
            this.auditService = auditService;
        }

        [RequireAccess("Permite consultar o histórico de auditoria de uma entidade específica.")]
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

        [RequireAccess("Permite consultar os detalhes de um registro específico da auditoria.")]
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
    }
}
