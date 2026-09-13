using System.ComponentModel.DataAnnotations;
using Archon.Api.Attributes;
using Archon.Api.Contracts.Bulk;
using Archon.Api.Controllers;
using Archon.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Archon.Testing.Integration.Support
{
    public sealed class TestApiController : ApiControllerBase
    {
        [GetEndpoint("success")]
        public IActionResult Success()
        {
            return Http200(new
            {
                Value = "ok"
            }, "Completed.");
        }

        [GetEndpoint("tenant")]
        public IActionResult Tenant()
        {
            return Http200(new
            {
                TenantId = HttpContext.Items["TenantId"]?.ToString()
            });
        }

        [GetEndpoint("failure")]
        public IActionResult Failure()
        {
            throw new InvalidOperationException("Invalid request.");
        }

        [PostEndpoint]
        public IActionResult ValidateRequest([FromBody] TestRequest request)
        {
            return Http200(request, "Validated.");
        }

        // Lote de teste: id 2 viola regra com chave do catalogo, id 3 lanca texto que nao e chave, o resto passa.
        [DeleteEndpoint("bulk")]
        public Task<IActionResult> DeleteMany([FromBody] BulkIdsRequest request, CancellationToken cancellationToken)
        {
            return ExecuteBulk(request, (id, _) => id switch
            {
                2 => throw new BusinessRuleException("record.notFound"),
                3 => throw new InvalidOperationException("texto livre que nao e chave"),
                _ => Task.CompletedTask
            }, cancellationToken);
        }

        [GetEndpoint("{id}")]
        public IActionResult GetById(string id)
        {
            return Http200(new
            {
                Id = id
            });
        }
    }

    public sealed class TestRequest
    {
        [Required]
        public string Name { get; init; } = string.Empty;
    }
}
