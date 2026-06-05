using Archon.Api.Attributes;
using Archon.Application.Integrations;
using Archon.Infrastructure.Integrations;
using Microsoft.AspNetCore.Mvc;

namespace Archon.Api.Controllers
{
    public sealed class TenantsController : ApiControllerBase
    {
        private readonly ITenantBootstrapService tenantBootstrapService;

        public TenantsController(ITenantBootstrapService tenantBootstrapService)
        {
            this.tenantBootstrapService = tenantBootstrapService;
        }

        [RequireAccess("tenants.bootstrap.description")]
        [PostEndpoint("bootstrap")]
        public async Task<IActionResult> Bootstrap([FromBody] TenantBootstrapRequest request, CancellationToken cancellationToken)
        {
            IActionResult? validationResult = ValidateBody(request);
            if (validationResult is not null)
            {
                return validationResult;
            }

            TenantBootstrapResult result = await tenantBootstrapService.BootstrapAsync(request, cancellationToken);
            return Http200(result);
        }
    }
}
