using Archon.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Archon.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public abstract class ApiControllerBase : ControllerBase
    {
        protected ICurrentUser CurrentUser => HttpContext.RequestServices.GetRequiredService<ICurrentUser>();

        protected long? CurrentUserId => CurrentUser.UserId;

        protected string? CurrentUserName => CurrentUser.UserName;

        protected string? CurrentUserEmail => CurrentUser.Email;

        protected string? CurrentClientId => CurrentUser.ClientId;

        protected virtual IActionResult? ValidateBody(object? body)
        {
            if (body is null)
            {
                return Http400(new { message = "Request body is required." });
            }

            if (!ModelState.IsValid)
            {
                return Http400(ModelState);
            }

            return null;
        }

        protected IActionResult Http200(object? data = null)
        {
            return data is null ? Ok() : Ok(data);
        }

        protected IActionResult Http201(object? data = null)
        {
            return StatusCode(StatusCodes.Status201Created, data);
        }

        protected IActionResult Http202(object? data = null)
        {
            return StatusCode(StatusCodes.Status202Accepted, data);
        }

        protected IActionResult Http204()
        {
            return NoContent();
        }

        protected IActionResult Http400(object error)
        {
            return BadRequest(error);
        }

        protected IActionResult Http401(object? error = null)
        {
            return error is null ? Unauthorized() : Unauthorized(error);
        }

        protected IActionResult Http403(object? error = null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, error);
        }

        protected IActionResult Http404(object? error = null)
        {
            return error is null ? NotFound() : NotFound(error);
        }

        protected IActionResult Http409(object error)
        {
            return Conflict(error);
        }

        protected IActionResult Http412(object error)
        {
            return StatusCode(StatusCodes.Status412PreconditionFailed, error);
        }

        protected IActionResult Http422(object error)
        {
            return UnprocessableEntity(error);
        }

        protected IActionResult Http500(object error)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, error);
        }

        protected IActionResult SendFile(byte[] content, string contentType, string fileName)
        {
            if (content.Length == 0)
            {
                return Http400(new { message = "File content is required." });
            }

            return File(content, contentType, fileName);
        }

        protected IActionResult SendFile(Stream content, string contentType, string fileName)
        {
            if (content.Length == 0)
            {
                return Http400(new { message = "File content is required." });
            }

            return File(content, contentType, fileName);
        }

        protected IActionResult SendPdf(byte[] content, string fileName)
        {
            return SendFile(content, "application/pdf", fileName);
        }

        protected IActionResult SendExcel(byte[] content, string fileName)
        {
            return SendFile(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        protected IActionResult SendCsv(byte[] content, string fileName)
        {
            return SendFile(content, "text/csv", fileName);
        }
    }
}
