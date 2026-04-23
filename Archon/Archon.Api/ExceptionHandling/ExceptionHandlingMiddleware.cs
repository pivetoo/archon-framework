using System.Diagnostics;
using Archon.Core.Exceptions;
using Archon.Core.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
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
            IStringLocalizer<ArchonApiResource> localizer = context.RequestServices.GetRequiredService<IStringLocalizer<ArchonApiResource>>();
            ILogger<ExceptionHandlingMiddleware> logger = context.RequestServices.GetRequiredService<ILogger<ExceptionHandlingMiddleware>>();

            try
            {
                await next(context);
            }
            catch (UnauthorizedAccessException exception)
            {
                logger.LogWarning(exception, "Unauthorized access attempt at {Path}", context.Request.Path);
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, Translate(localizer, exception.Message));
            }
            catch (KeyNotFoundException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status404NotFound, Translate(localizer, exception.Message));
            }
            catch (InvalidOperationException exception) when (IsClientError(exception.Message))
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, Translate(localizer, exception.Message));
            }
            catch (InvalidOperationException exception)
            {
                logger.LogError(exception, "Internal invalid operation at {Path}", context.Request.Path);
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, localizer["error.unexpected"]);
            }
            catch (ArgumentException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, Translate(localizer, exception.Message));
            }
            catch (IntegrityException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status409Conflict, Translate(localizer, exception.Message));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected error at {Path}: {Message}", context.Request.Path, exception.Message);
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, localizer["error.unexpected"]);
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
                "tenant."
            ];

            return clientErrorPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static string Translate(IStringLocalizer<ArchonApiResource> localizer, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return localizer["error.unexpected.short"];
            }

            LocalizedString localized = localizer[message];
            return localized.ResourceNotFound ? message : localized.Value;
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
