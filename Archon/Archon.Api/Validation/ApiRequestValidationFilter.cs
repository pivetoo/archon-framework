using Archon.Api.Controllers;
using Archon.Api.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using System.Reflection;

namespace Archon.Api.Validation
{
    public sealed class ApiRequestValidationFilter : IAsyncActionFilter
    {
        private static readonly NullabilityInfoContext NullabilityInfoContext = new();

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.Controller is not ApiControllerBase)
            {
                await next();
                return;
            }

            if (!TryGetBodyParameter(context, out bool bodyRequired, out object? body))
            {
                await next();
                return;
            }

            IStringLocalizer<ArchonApiResource> localizer = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<ArchonApiResource>>();
            IActionResult? validationResult = ApiRequestValidator.Validate(body, bodyRequired, context.ModelState, localizer);
            if (validationResult is not null)
            {
                context.Result = validationResult;
                return;
            }

            await next();
        }

        private static bool TryGetBodyParameter(ActionExecutingContext context, out bool bodyRequired, out object? body)
        {
            foreach (ControllerParameterDescriptor parameter in context.ActionDescriptor.Parameters.OfType<ControllerParameterDescriptor>())
            {
                if (parameter.BindingInfo?.BindingSource != BindingSource.Body)
                {
                    continue;
                }

                bodyRequired = RequiresBody(parameter);
                body = context.ActionArguments.TryGetValue(parameter.Name, out object? value) ? value : null;
                return true;
            }

            bodyRequired = false;
            body = null;
            return false;
        }

        private static bool RequiresBody(ControllerParameterDescriptor parameter)
        {
            if (parameter.ParameterInfo.HasDefaultValue)
            {
                return false;
            }

            Type parameterType = parameter.ParameterInfo.ParameterType;
            if (parameterType.IsValueType)
            {
                return Nullable.GetUnderlyingType(parameterType) is null;
            }

            NullabilityInfo nullability = NullabilityInfoContext.Create(parameter.ParameterInfo);
            return nullability.ReadState != NullabilityState.Nullable;
        }
    }
}
