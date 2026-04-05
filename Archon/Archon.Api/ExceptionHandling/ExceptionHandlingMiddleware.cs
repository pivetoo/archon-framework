using Archon.Core.Exceptions;
using Archon.Core.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
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

            try
            {
                await next(context);
            }
            catch (UnauthorizedAccessException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, Translate(localizer, exception.Message));
            }
            catch (KeyNotFoundException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status404NotFound, Translate(localizer, exception.Message));
            }
            catch (InvalidOperationException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, Translate(localizer, exception.Message));
            }
            catch (ArgumentException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, Translate(localizer, exception.Message));
            }
            catch (IntegrityException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status409Conflict, Translate(localizer, exception.Message));
            }
            catch
            {
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, localizer["error.unexpected"]);
            }
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
            return context.Response.WriteAsJsonAsync(new ApiResponse
            {
                Message = message
            });
        }
    }
}
