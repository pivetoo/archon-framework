using Archon.Api.Contracts.Bulk;
using Archon.Api.Localization;
using Archon.Api.Validation;
using Archon.Application.Abstractions;
using Archon.Application.Services;
using Archon.Core.Bulk;
using Archon.Core.Entities;
using Archon.Core.Pagination;
using Archon.Core.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;

namespace Archon.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public abstract class ApiControllerBase : ControllerBase
    {
        protected ICurrentUser CurrentUser => HttpContext.RequestServices.GetRequiredService<ICurrentUser>();

        protected IStringLocalizer<ArchonApiResource> Localizer => HttpContext.RequestServices.GetRequiredService<IStringLocalizer<ArchonApiResource>>();

        protected long? CurrentUserId => CurrentUser.UserId;

        protected string? CurrentUserName => CurrentUser.UserName;

        protected string? CurrentUserEmail => CurrentUser.Email;

        protected string? CurrentClientId => CurrentUser.ClientId;

        protected string RequestIpAddress => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        protected string RequestUserAgent => Request.Headers.UserAgent.FirstOrDefault() ?? "unknown";

        protected virtual IActionResult? ValidateBody(object? body)
        {
            return ApiRequestValidator.Validate(body, bodyRequired: true, ModelState, Localizer);
        }

        /// <summary>
        /// Executa <paramref name="operation"/> para cada id do lote e responde com o resultado por registro.
        /// Use para reaproveitar o metodo de servico que ja aplica as regras (ex.: excluir um registro).
        /// </summary>
        protected async Task<IActionResult> ExecuteBulk(BulkIdsRequest? request, Func<long, CancellationToken, Task> operation, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operation);

            if (!TryReadBulkIds(request, out IReadOnlyList<long> ids, out IActionResult? invalid))
            {
                return invalid!;
            }

            IBulkOperationRunner runner = HttpContext.RequestServices.GetRequiredService<IBulkOperationRunner>();
            BulkOperationResult result = await runner.RunAsync(ids, operation, cancellationToken);
            return BulkResponse(result);
        }

        /// <summary>
        /// Ativa ou inativa os registros do lote carregando cada entidade do banco. O cliente manda so os ids,
        /// entao nenhum outro campo do registro pode ser sobrescrito por dado velho da tela.
        /// </summary>
        protected async Task<IActionResult> SetActiveBulk<T>(BulkIdsRequest? request, bool isActive, CancellationToken cancellationToken, Func<T, CancellationToken, Task>? validate = null)
            where T : Entity, IActivatable
        {
            if (!TryReadBulkIds(request, out IReadOnlyList<long> ids, out IActionResult? invalid))
            {
                return invalid!;
            }

            IBulkOperationRunner runner = HttpContext.RequestServices.GetRequiredService<IBulkOperationRunner>();
            BulkOperationResult result = await runner.SetActiveAsync(ids, isActive, validate, cancellationToken);
            return BulkResponse(result);
        }

        /// <summary>
        /// Resposta padrao de lote: HTTP 200 com o resultado por registro, mesmo quando algum falha, porque a
        /// requisicao em si foi processada. A mensagem resume o que aconteceu e cada falha vem traduzida.
        /// </summary>
        protected IActionResult BulkResponse(BulkOperationResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            IStringLocalizerFactory factory = HttpContext.RequestServices.GetRequiredService<IStringLocalizerFactory>();
            LocalizationCatalogOptions catalog = HttpContext.RequestServices.GetRequiredService<LocalizationCatalogOptions>();
            ILogger logger = HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());

            List<BulkOperationFailureContract> failures = result.Failures
                .Select(failure =>
                {
                    if (!LocalizedMessageResolver.TryResolve(Localizer, factory, catalog, failure.MessageKey, failure.MessageArgs.ToArray(), out string message))
                    {
                        logger.LogWarning("Chave de localizacao ausente no catalogo: {Key}. O lote devolveu a mensagem generica.", failure.MessageKey);
                        message = Localizer["error.unexpected.short"];
                    }

                    return new BulkOperationFailureContract { Id = failure.Id, Message = message };
                })
                .ToList();

            string summary = result.Failed == 0
                ? Localizer["bulk.result.completed", result.Succeeded]
                : result.Succeeded == 0
                    ? Localizer["bulk.result.failed", result.Failed]
                    : Localizer["bulk.result.partial", result.Succeeded, result.Total, result.Failed];

            return Http200(new BulkOperationResultContract
            {
                Total = result.Total,
                Succeeded = result.Succeeded,
                Failed = result.Failed,
                SucceededIds = result.SucceededIds,
                Failures = failures
            }, summary);
        }

        private bool TryReadBulkIds(BulkIdsRequest? request, out IReadOnlyList<long> ids, out IActionResult? invalid)
        {
            ids = [];
            invalid = null;

            if (request?.Ids is null || request.Ids.Count == 0)
            {
                invalid = Http400(Localizer["bulk.ids.required"]);
                return false;
            }

            if (request.Ids.Any(id => id <= 0))
            {
                invalid = Http400(Localizer["bulk.ids.invalid"]);
                return false;
            }

            List<long> distinct = request.Ids.Distinct().ToList();
            if (distinct.Count > BulkIdsRequest.MaxItems)
            {
                invalid = Http400(Localizer["bulk.ids.tooMany", BulkIdsRequest.MaxItems]);
                return false;
            }

            ids = distinct;
            return true;
        }

        protected IActionResult Http200(object? data = null, string? message = null)
        {
            return StatusCode(StatusCodes.Status200OK, CreateResponse(message, data));
        }

        protected IActionResult Http200<T>(T data, string? message = null)
        {
            return StatusCode(StatusCodes.Status200OK, CreateResponse<T>(message, data));
        }

        protected IActionResult Http200<T>(PagedResult<T> pagedResult, string? message = null)
        {
            return StatusCode(StatusCodes.Status200OK, CreateResponse(message, pagedResult.Items, pagination: pagedResult.Pagination));
        }

        protected IActionResult Http201(object? data = null, string? message = null)
        {
            return StatusCode(StatusCodes.Status201Created, CreateResponse(message, data));
        }

        protected IActionResult Http201<T>(T data, string? message = null)
        {
            return StatusCode(StatusCodes.Status201Created, CreateResponse<T>(message, data));
        }

        protected IActionResult Http202(object? data = null, string? message = null)
        {
            return StatusCode(StatusCodes.Status202Accepted, CreateResponse(message, data));
        }

        protected IActionResult Http202<T>(T data, string? message = null)
        {
            return StatusCode(StatusCodes.Status202Accepted, CreateResponse<T>(message, data));
        }

        protected IActionResult Http204()
        {
            return StatusCode(StatusCodes.Status200OK, CreateResponse(Localizer["operation.completed"], data: new { ok = true }));
        }

        protected IActionResult Http400(string message, object? errors = null)
        {
            return StatusCode(StatusCodes.Status400BadRequest, CreateResponse(message, errors: errors));
        }

        protected IActionResult Http401(string? message = null, object? errors = null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, CreateResponse(message ?? Localizer["auth.unauthorized"], errors: errors));
        }

        protected IActionResult Http403(string? message = null, object? errors = null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateResponse(message ?? Localizer["auth.forbidden"], errors: errors));
        }

        protected IActionResult Http404(string? message = null, object? errors = null)
        {
            return StatusCode(StatusCodes.Status404NotFound, CreateResponse(message ?? Localizer["record.notFound"], errors: errors));
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
            return StatusCode(StatusCodes.Status422UnprocessableEntity, CreateResponse(message ?? Localizer["validation.failed"], errors: errors));
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
                return Http400(Localizer["file.content.required"]);
            }

            return File(content, contentType, fileName);
        }

        protected IActionResult SendFile(Stream content, string contentType, string fileName)
        {
            if (content.Length == 0)
            {
                return Http400(Localizer["file.content.required"]);
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

        internal static ApiResponse CreateResponse(string? message = null, object? data = null, object? errors = null, object? pagination = null)
        {
            return new ApiResponse
            {
                Message = message ?? string.Empty,
                Data = data,
                Errors = errors,
                Pagination = pagination
            };
        }

        internal static ApiResponse<T> CreateResponse<T>(string? message = null, T? data = default, object? errors = null, object? pagination = null)
        {
            return new ApiResponse<T>
            {
                Message = message ?? string.Empty,
                Data = data,
                Errors = errors,
                Pagination = pagination
            };
        }

        internal static Dictionary<string, IReadOnlyCollection<string>> NormalizeModelStateErrors(ModelStateDictionary modelState, IStringLocalizer<ArchonApiResource> localizer)
        {
            return modelState
                .Where(item => item.Value?.Errors.Count > 0)
                .ToDictionary(
                    item => item.Key,
                    item => (IReadOnlyCollection<string>)item.Value!.Errors
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? localizer["validation.invalidValue"].Value : error.ErrorMessage)
                        .ToList());
        }

        protected IReadOnlyCollection<string> NormalizeExceptions(IReadOnlyCollection<Exception> errors)
        {
            return errors
                .Select(error => string.IsNullOrWhiteSpace(error.Message) ? Localizer["error.unexpected.short"].Value : error.Message)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }
}
