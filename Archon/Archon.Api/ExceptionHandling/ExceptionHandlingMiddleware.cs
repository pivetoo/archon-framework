using Archon.Api.Localization;
using Archon.Core.Exceptions;
using Archon.Core.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Diagnostics;

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
            LocalizationCatalogOptions catalog = context.RequestServices.GetRequiredService<LocalizationCatalogOptions>();
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
            catch (InvalidOperationException exception)
            {
                if (TryResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message, null, out string resolved))
                {
                    logger.LogInformation("400 Bad Request {Method} {Path} traceId={TraceId} key={Key}",
                        context.Request.Method, context.Request.Path, traceId, exception.Message);
                    await WriteErrorAsync(context, StatusCodes.Status400BadRequest, resolved);
                }
                else
                {
                    logger.LogError(exception, "500 Internal Server Error {Method} {Path} traceId={TraceId}",
                        context.Request.Method, context.Request.Path, traceId);
                    await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, archonLocalizer["error.unexpected"]);
                }
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

        private static bool TryResolveMessage(
            IStringLocalizer<ArchonApiResource> archonLocalizer,
            IStringLocalizerFactory factory,
            LocalizationCatalogOptions catalog,
            string? message,
            object[]? args,
            out string resolved)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                resolved = archonLocalizer["error.unexpected.short"];
                return false;
            }

            object[] formatArgs = args ?? [];

            foreach (Type resourceType in catalog.ResourceTypes)
            {
                IStringLocalizer appLocalizer = factory.Create(resourceType);
                LocalizedString localized = formatArgs.Length > 0
                    ? appLocalizer[message, formatArgs]
                    : appLocalizer[message];
                if (!localized.ResourceNotFound)
                {
                    resolved = localized.Value;
                    return true;
                }
            }

            LocalizedString archon = formatArgs.Length > 0
                ? archonLocalizer[message, formatArgs]
                : archonLocalizer[message];
            if (!archon.ResourceNotFound)
            {
                resolved = archon.Value;
                return true;
            }

            resolved = message;
            return false;
        }

        private static string ResolveMessage(
            IStringLocalizer<ArchonApiResource> archonLocalizer,
            IStringLocalizerFactory factory,
            LocalizationCatalogOptions catalog,
            string message,
            object[]? args = null)
        {
            TryResolveMessage(archonLocalizer, factory, catalog, message, args, out string resolved);
            return resolved;
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
