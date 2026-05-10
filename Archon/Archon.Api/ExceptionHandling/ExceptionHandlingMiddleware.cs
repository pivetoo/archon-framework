using System.Diagnostics;
using Archon.Core.Exceptions;
using Archon.Core.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Archon.Api.Localization;

namespace Archon.Api.ExceptionHandling
{
    public sealed class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate next;

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            IStringLocalizer<ArchonApiResource> archonLocalizer = context.RequestServices.GetRequiredService<IStringLocalizer<ArchonApiResource>>();
            ILogger<ExceptionHandlingMiddleware> logger = context.RequestServices.GetRequiredService<ILogger<ExceptionHandlingMiddleware>>();
            LocalizationCatalogOptions catalog = context.RequestServices.GetRequiredService<IOptions<LocalizationCatalogOptions>>().Value;
            IStringLocalizerFactory localizerFactory = context.RequestServices.GetRequiredService<IStringLocalizerFactory>();

            string traceId = Activity.Current?.TraceId.ToString() ?? string.Empty;

            try
            {
                await next(context);
            }
            // ============ Exceptions tipadas (preferenciais) ============
            catch (NotFoundException exception)
            {
                logger.LogInformation("404 Not Found {Method} {Path} traceId={TraceId} key={Key}",
                    context.Request.Method, context.Request.Path, traceId, exception.Message);
                await WriteErrorAsync(context, StatusCodes.Status404NotFound, ResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message, exception.MessageArgs));
            }
            catch (ConflictException exception)
            {
                logger.LogWarning("409 Conflict {Method} {Path} traceId={TraceId} key={Key}",
                    context.Request.Method, context.Request.Path, traceId, exception.Message);
                await WriteErrorAsync(context, StatusCodes.Status409Conflict, ResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message, exception.MessageArgs));
            }
            catch (ForbiddenException exception)
            {
                logger.LogWarning("403 Forbidden {Method} {Path} traceId={TraceId} key={Key}",
                    context.Request.Method, context.Request.Path, traceId, exception.Message);
                await WriteErrorAsync(context, StatusCodes.Status403Forbidden, ResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message, exception.MessageArgs));
            }
            catch (BusinessRuleException exception)
            {
                logger.LogInformation("400 Business Rule {Method} {Path} traceId={TraceId} key={Key}",
                    context.Request.Method, context.Request.Path, traceId, exception.Message);
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, ResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message, exception.MessageArgs));
            }
            // ============ Exceptions legadas (compatibilidade) ============
            catch (UnauthorizedAccessException exception)
            {
                logger.LogWarning(exception, "401 Unauthorized {Method} {Path} traceId={TraceId} message={Message}",
                    context.Request.Method, context.Request.Path, traceId, exception.Message);
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, ResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message));
            }
            catch (KeyNotFoundException exception)
            {
                logger.LogInformation("404 Not Found {Method} {Path} traceId={TraceId} message={Message}",
                    context.Request.Method, context.Request.Path, traceId, exception.Message);
                await WriteErrorAsync(context, StatusCodes.Status404NotFound, ResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message));
            }
            catch (InvalidOperationException exception) when (IsClientError(exception.Message))
            {
                logger.LogInformation("400 Bad Request {Method} {Path} traceId={TraceId} message={Message}",
                    context.Request.Method, context.Request.Path, traceId, exception.Message);
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, ResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message));
            }
            catch (InvalidOperationException exception)
            {
                logger.LogError(exception, "500 Internal Server Error {Method} {Path} traceId={TraceId}",
                    context.Request.Method, context.Request.Path, traceId);
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, archonLocalizer["error.unexpected"]);
            }
            catch (ArgumentException exception)
            {
                logger.LogWarning(exception, "400 Bad Request (ArgumentException) {Method} {Path} traceId={TraceId} message={Message}",
                    context.Request.Method, context.Request.Path, traceId, exception.Message);
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, ResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message));
            }
            catch (IntegrityException exception)
            {
                logger.LogWarning(exception, "409 Conflict {Method} {Path} traceId={TraceId} message={Message}",
                    context.Request.Method, context.Request.Path, traceId, exception.Message);
                await WriteErrorAsync(context, StatusCodes.Status409Conflict, ResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "500 Internal Server Error {Method} {Path} traceId={TraceId}",
                    context.Request.Method, context.Request.Path, traceId);
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, archonLocalizer["error.unexpected"]);
            }
        }

        private static bool IsClientError(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string normalized = message.Trim().ToLowerInvariant();
            string[] clientErrorPrefixes =
            [
                "request.",
                "validation.",
                "record.",
                "error.",
                "auth.",
                "tenant.",
                "user.",
                "role.",
                "contract.",
                "company.",
                "proposal.",
                "opportunity.",
                "campaign.",
                "creator.",
                "brand.",
                "financial.",
                "deliverable.",
                "integration.",
                "automation.",
                "notification."
            ];

            return clientErrorPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static string ResolveMessage(
            IStringLocalizer<ArchonApiResource> archonLocalizer,
            IStringLocalizerFactory factory,
            LocalizationCatalogOptions catalog,
            string message,
            object[]? args = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return archonLocalizer["error.unexpected.short"];
            }

            object[] formatArgs = args ?? [];

            // Tenta primeiro nos resources da aplicacao consumidora (mais especificos)
            foreach (Type resourceType in catalog.ResourceTypes)
            {
                IStringLocalizer appLocalizer = factory.Create(resourceType);
                LocalizedString localized = formatArgs.Length > 0
                    ? appLocalizer[message, formatArgs]
                    : appLocalizer[message];
                if (!localized.ResourceNotFound)
                {
                    return localized.Value;
                }
            }

            // Cai no resource do Archon
            LocalizedString archon = formatArgs.Length > 0
                ? archonLocalizer[message, formatArgs]
                : archonLocalizer[message];
            return archon.ResourceNotFound ? message : archon.Value;
        }

        private static Task WriteErrorAsync(HttpContext context, int statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            ProblemDetails problemDetails = new()
            {
                Type = $"https://api.archon.dev/errors/{statusCode}",
                Title = GetTitleForStatusCode(statusCode),
                Status = statusCode,
                Detail = message,
                Instance = context.Request.Path
            };

            if (Activity.Current?.TraceId is ActivityTraceId traceId)
            {
                problemDetails.Extensions["traceId"] = traceId.ToString();
            }

            return context.Response.WriteAsJsonAsync(new ApiResponse
            {
                Message = message,
                Errors = problemDetails
            });
        }

        private static string GetTitleForStatusCode(int statusCode)
        {
            return statusCode switch
            {
                StatusCodes.Status400BadRequest => "Bad Request",
                StatusCodes.Status401Unauthorized => "Unauthorized",
                StatusCodes.Status403Forbidden => "Forbidden",
                StatusCodes.Status404NotFound => "Not Found",
                StatusCodes.Status409Conflict => "Conflict",
                StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
                StatusCodes.Status500InternalServerError => "Internal Server Error",
                _ => "Error"
            };
        }
    }
}
