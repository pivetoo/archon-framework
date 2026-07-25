using Archon.Core.Entities;
using Archon.Core.Pagination;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Archon.Infrastructure.Persistence.EF
{
    public static class QueryablePaginationExtensions
    {
        private static readonly string[] OrderingMethods =
        [
            nameof(Queryable.OrderBy),
            nameof(Queryable.OrderByDescending),
            nameof(Queryable.ThenBy),
            nameof(Queryable.ThenByDescending)
        ];

        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PagedRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(request);

            long totalCount = await query.LongCountAsync(cancellationToken);

            List<T> items = await EnsureDeterministicOrder(query)
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

        /// <summary>
        /// `Skip`/`Take` sem `ORDER BY` nao tem ordem garantida: o banco pode devolver o mesmo registro
        /// em duas paginas e omitir outro. Como este e o helper de paginacao padrao, qualquer listagem
        /// que esqueca o `OrderBy` herdava isso em silencio.
        ///
        /// Quando a consulta ja tem ordenacao, nada muda. Sem ordenacao e sendo entidade, ordena por
        /// `Id` — criterio arbitrario, mas estavel, que e o que importa para paginar.
        /// </summary>
        private static IQueryable<T> EnsureDeterministicOrder<T>(IQueryable<T> query)
        {
            if (HasOrdering(query.Expression))
            {
                return query;
            }

            // Projecao (tipo anonimo, model) nao tem chave conhecida para ordenar sozinha. Nesses casos
            // a ordenacao continua sendo responsabilidade de quem chama.
            if (!typeof(Entity).IsAssignableFrom(typeof(T)))
            {
                return query;
            }

            ParameterExpression parameter = Expression.Parameter(typeof(T), "entity");
            MemberExpression idProperty = Expression.Property(parameter, nameof(Entity.Id));
            LambdaExpression keySelector = Expression.Lambda(idProperty, parameter);

            MethodCallExpression orderByCall = Expression.Call(
                typeof(Queryable),
                nameof(Queryable.OrderBy),
                [typeof(T), typeof(long)],
                query.Expression,
                Expression.Quote(keySelector));

            return query.Provider.CreateQuery<T>(orderByCall);
        }

        /// <summary>
        /// Percorre a cadeia da expressao. Checar `is IOrderedQueryable&lt;T&gt;` nao serve: um
        /// `OrderBy(...).Where(...)` volta como `IQueryable`, e o helper acharia que nao ha ordenacao —
        /// acrescentaria um segundo `OrderBy` e sobrescreveria a ordem pedida por quem chamou.
        /// </summary>
        private static bool HasOrdering(Expression expression)
        {
            if (expression is not MethodCallExpression call)
            {
                return false;
            }

            if (OrderingMethods.Contains(call.Method.Name, StringComparer.Ordinal))
            {
                return true;
            }

            return call.Arguments.Count > 0 && HasOrdering(call.Arguments[0]);
        }
    }
}
