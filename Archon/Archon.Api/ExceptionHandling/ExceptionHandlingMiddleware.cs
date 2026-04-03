using Archon.Core.Exceptions;
using Archon.Core.Responses;
using Microsoft.AspNetCore.Http;

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
            try
            {
                await next(context);
            }
            catch (UnauthorizedAccessException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, exception.Message);
            }
            catch (KeyNotFoundException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status404NotFound, exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, exception.Message);
            }
            catch (ArgumentException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, exception.Message);
            }
            catch (IntegrityException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status409Conflict, exception.Message);
            }
            catch
            {
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.");
            }
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
