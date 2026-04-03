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
        public virtual IActionResult Post([FromBody] T entity)
        {
            IActionResult? validationResult = ValidateBody(entity);
            if (validationResult is not null)
            {
                return validationResult;
            }

            bool success = Service.Insert(entity);
            if (!success)
            {
                return Http422(Service.Messages);
            }

            return Http201(entity);
        }

        [PutEndpoint]
        public virtual IActionResult Put([FromBody] T entity)
        {
            IActionResult? validationResult = ValidateBody(entity);
            if (validationResult is not null)
            {
                return validationResult;
            }

            T? result = Service.Update(entity);
            if (result is null)
            {
                return Http422(Service.Messages);
            }

            return Http200(result);
        }

        [DeleteEndpoint("{id:long}")]
        public virtual IActionResult Delete(long id)
        {
            T? result = Service.Delete(id);
            if (result is null)
            {
                return Http422(Service.Messages);
            }

            return Http200(result);
        }
    }
}
