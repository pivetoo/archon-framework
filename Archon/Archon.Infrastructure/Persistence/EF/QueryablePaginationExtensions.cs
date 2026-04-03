using Archon.Core.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Archon.Infrastructure.Persistence.EF
{
    public static class QueryablePaginationExtensions
    {
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PagedRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(request);

            long totalCount = await query.LongCountAsync(cancellationToken);
            List<T> items = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            int totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)request.PageSize);

            return new PagedResult<T>
            {
                Items = items,
                Pagination = new PaginationMetadata
                {
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalCount = totalCount,
                    TotalPages = totalPages
                }
            };
        }

        public static async Task<PagedResult<TResult>> ToPagedResultAsync<TSource, TResult>(this IQueryable<TSource> query, PagedRequest request, Func<TSource, TResult> selector, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(selector);

            PagedResult<TSource> pagedResult = await query.ToPagedResultAsync(request, cancellationToken);

            return new PagedResult<TResult>
            {
                Items = pagedResult.Items.Select(selector).ToList(),
                Pagination = pagedResult.Pagination
            };
        }
    }
}
