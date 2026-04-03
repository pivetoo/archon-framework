using Archon.Api.Attributes;
using Archon.Application.Services;
using Archon.Core.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Archon.Api.Controllers
{
    public abstract class ApiControllerCrud<T> : ApiControllerBase where T : Entity
    {
        protected ICrudService<T> Service { get; }

        protected ApiControllerCrud(ICrudService<T> service)
        {
            Service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [PostEndpoint]
        public virtual async Task<IActionResult> Post([FromBody] T entity, CancellationToken cancellationToken)
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

        [PutEndpoint]
        public virtual async Task<IActionResult> Put([FromBody] T entity, CancellationToken cancellationToken)
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

        [DeleteEndpoint("{id:long}")]
        public virtual async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
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
