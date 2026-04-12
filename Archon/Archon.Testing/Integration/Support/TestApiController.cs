using System.ComponentModel.DataAnnotations;
using Archon.Api.Attributes;
using Archon.Api.Controllers;
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
    }

    public sealed class TestRequest
    {
        [Required]
        public string Name { get; init; } = string.Empty;
    }
}
