using Archon.Api.Attributes;
using Archon.Application.Services;
using Archon.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archon.Api.Controllers
{
    [RequireAccess]
    public abstract class ApiControllerCrud<T> : ReadOnlyController<T> where T : Entity
    {
        protected ICrudService<T> Service { get; }

        protected ApiControllerCrud(ICrudService<T> service, DbContext dbContext) : base(dbContext)
        {
            Service = service ?? throw new ArgumentNullException(nameof(service));
        }

        protected virtual async Task<IActionResult> Create(T entity, CancellationToken cancellationToken)
        {
            IActionResult? validationResult = ValidateBody(entity);
            if (validationResult is not null)
            {
                return validationResult;
            }

            bool success = await Service.Insert(cancellationToken, entity);
            if (!success)
            {
                return Http422(Service.Messages);
            }

            return Http201(entity, "Record created successfully.");
        }

        protected virtual async Task<IActionResult> Update(T entity, CancellationToken cancellationToken)
        {
            IActionResult? validationResult = ValidateBody(entity);
            if (validationResult is not null)
            {
                return validationResult;
            }

            T? result = await Service.Update(entity, cancellationToken);
            if (result is null)
            {
                return ResolveServiceError();
            }

            return Http200(result, "Record updated successfully.");
        }

        protected virtual async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
        {
            T? result = await Service.Delete(id, cancellationToken);
            if (result is null)
            {
                return ResolveServiceError();
            }

            return Http200(result, "Record deleted successfully.");
        }

        protected virtual IActionResult ResolveServiceError()
        {
            if (Service.Messages.Any(message => message is KeyNotFoundException))
            {
                return Http404("Record not found.", NormalizeExceptions(Service.Messages));
            }

            return Http422(Service.Messages);
        }
    }
}
