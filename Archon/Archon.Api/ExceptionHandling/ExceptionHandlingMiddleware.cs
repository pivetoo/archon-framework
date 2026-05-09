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

            try
            {
                await next(context);
            }
            catch (UnauthorizedAccessException exception)
            {
                logger.LogWarning(exception, "Unauthorized access attempt at {Path}", context.Request.Path);
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, ResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message));
            }
            catch (KeyNotFoundException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status404NotFound, ResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message));
            }
            catch (InvalidOperationException exception) when (IsClientError(exception.Message))
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, ResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message));
            }
            catch (InvalidOperationException exception)
            {
                logger.LogError(exception, "Internal invalid operation at {Path}", context.Request.Path);
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, archonLocalizer["error.unexpected"]);
            }
            catch (ArgumentException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, ResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message));
            }
            catch (IntegrityException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status409Conflict, ResolveMessage(archonLocalizer, localizerFactory, catalog, exception.Message));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected error at {Path}: {Message}", context.Request.Path, exception.Message);
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
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return archonLocalizer["error.unexpected.short"];
            }

            // Tenta primeiro nos resources da aplicacao consumidora (mais especificos)
            foreach (Type resourceType in catalog.ResourceTypes)
            {
                IStringLocalizer appLocalizer = factory.Create(resourceType);
                LocalizedString localized = appLocalizer[message];
                if (!localized.ResourceNotFound)
                {
                    return localized.Value;
                }
            }

            // Cai no resource do Archon
            LocalizedString archon = archonLocalizer[message];
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
