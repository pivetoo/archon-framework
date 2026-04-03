using Archon.Application.Abstractions;
using Archon.Core.Pagination;
using Archon.Core.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
                return Http400("Request body is required.");
            }

            if (!ModelState.IsValid)
            {
                return Http400("Validation failed.", NormalizeModelStateErrors(ModelState));
            }

            return null;
        }

        protected IActionResult Http200(object? data = null, string? message = null)
        {
            return StatusCode(StatusCodes.Status200OK, CreateResponse(message, data));
        }

        protected IActionResult Http200<T>(PagedResult<T> pagedResult, string? message = null)
        {
            return StatusCode(StatusCodes.Status200OK, CreateResponse(message, pagedResult.Items, pagination: pagedResult.Pagination));
        }

        protected IActionResult Http201(object? data = null, string? message = null)
        {
            return StatusCode(StatusCodes.Status201Created, CreateResponse(message, data));
        }

        protected IActionResult Http202(object? data = null, string? message = null)
        {
            return StatusCode(StatusCodes.Status202Accepted, CreateResponse(message, data));
        }

        protected IActionResult Http204()
        {
            return StatusCode(StatusCodes.Status200OK, CreateResponse("Operation completed successfully."));
        }

        protected IActionResult Http400(string message, object? errors = null)
        {
            return StatusCode(StatusCodes.Status400BadRequest, CreateResponse(message, errors: errors));
        }

        protected IActionResult Http401(string? message = null, object? errors = null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, CreateResponse(message ?? "Unauthorized.", errors: errors));
        }

        protected IActionResult Http403(string? message = null, object? errors = null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateResponse(message ?? "Forbidden.", errors: errors));
        }

        protected IActionResult Http404(string? message = null, object? errors = null)
        {
            return StatusCode(StatusCodes.Status404NotFound, CreateResponse(message ?? "Resource not found.", errors: errors));
        }

        protected IActionResult Http409(string message, object? errors = null)
        {
            return StatusCode(StatusCodes.Status409Conflict, CreateResponse(message, errors: errors));
        }

        protected IActionResult Http412(string message, object? errors = null)
        {
            return StatusCode(StatusCodes.Status412PreconditionFailed, CreateResponse(message, errors: errors));
        }

        protected IActionResult Http422(object errors, string? message = null)
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity, CreateResponse(message ?? "Validation failed.", errors: errors));
        }

        protected IActionResult Http422(IReadOnlyCollection<Exception> errors, string? message = null)
        {
            return Http422(NormalizeExceptions(errors), message);
        }

        protected IActionResult Http500(string message, object? errors = null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, CreateResponse(message, errors: errors));
        }

        protected IActionResult SendFile(byte[] content, string contentType, string fileName)
        {
            if (content.Length == 0)
            {
                return Http400("File content is required.");
            }

            return File(content, contentType, fileName);
        }

        protected IActionResult SendFile(Stream content, string contentType, string fileName)
        {
            if (content.Length == 0)
            {
                return Http400("File content is required.");
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

        private static ApiResponse CreateResponse(string? message = null, object? data = null, object? errors = null, object? pagination = null)
        {
            return new ApiResponse
            {
                Message = message ?? string.Empty,
                Data = data,
                Errors = errors,
                Pagination = pagination
            };
        }

        private static Dictionary<string, IReadOnlyCollection<string>> NormalizeModelStateErrors(ModelStateDictionary modelState)
        {
            return modelState
                .Where(item => item.Value?.Errors.Count > 0)
                .ToDictionary(
                    item => item.Key,
                    item => (IReadOnlyCollection<string>)item.Value!.Errors
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid value." : error.ErrorMessage)
                        .ToList());
        }

        protected static IReadOnlyCollection<string> NormalizeExceptions(IReadOnlyCollection<Exception> errors)
        {
            return errors
                .Select(error => string.IsNullOrWhiteSpace(error.Message) ? "Unexpected error." : error.Message)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }
}
