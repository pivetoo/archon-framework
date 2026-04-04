using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archon.Api.Controllers
{
    [AllowAnonymous]
    [Route("health")]
    public sealed class HealthController : ApiControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Http200(new
            {
                status = "healthy",
                timestamp = DateTimeOffset.UtcNow
            });
        }
    }
}
