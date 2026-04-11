using Archon.Api.Controllers;
using Archon.Api.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;

namespace Archon.Api.Validation
{
    internal static class ApiRequestValidator
    {
        public static IActionResult? Validate(object? body, bool bodyRequired, ModelStateDictionary modelState, IStringLocalizer<ArchonApiResource> localizer)
        {
            if (bodyRequired && body is null)
            {
                return new BadRequestObjectResult(ApiControllerBase.CreateResponse(localizer["request.body.required"]));
            }

            if (!modelState.IsValid)
            {
                return new BadRequestObjectResult(
                    ApiControllerBase.CreateResponse(
                        localizer["validation.failed"],
                        errors: ApiControllerBase.NormalizeModelStateErrors(modelState, localizer)));
            }

            return null;
        }
    }
}
