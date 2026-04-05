using Archon.Api.Attributes;
using Archon.Core.Entities;
using Archon.Core.Pagination;
using Archon.Infrastructure.Persistence.EF;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archon.Api.Controllers
{
    [RequireAccess]
    public abstract class ReadOnlyController<T> : ApiControllerBase where T : Entity
    {
        private readonly DbContext dbContext;

        protected ReadOnlyController(DbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        protected DbContext DbContext => dbContext;

        protected virtual async Task<IActionResult> Get(PagedRequest request, CancellationToken cancellationToken)
        {
            var result = await dbContext.Set<T>()
                .AsNoTracking()
                .OrderByDescending(item => item.Id)
                .ToPagedResultAsync(request, cancellationToken);

            return Http200(result);
        }

        protected virtual async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                return Http400(Localizer["request.id.required"]);
            }

            T? entity = await dbContext.Set<T>()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (entity is null)
            {
                return Http404(Localizer["record.notFound"]);
            }

            return Http200(entity);
        }
    }
}
