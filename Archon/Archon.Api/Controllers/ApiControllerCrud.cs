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

            return Http201(entity, Localizer["record.created"]);
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

            return Http200(result, Localizer["record.updated"]);
        }

        protected virtual async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
        {
            T? result = await Service.Delete(id, cancellationToken);
            if (result is null)
            {
                return ResolveServiceError();
            }

            return Http200(result, Localizer["record.deleted"]);
        }

        protected virtual IActionResult ResolveServiceError()
        {
            if (Service.Messages.Any(message => message is KeyNotFoundException))
            {
                return Http404(Localizer["record.notFound"], NormalizeExceptions(Service.Messages));
            }

            return Http422(Service.Messages);
        }
    }
}
