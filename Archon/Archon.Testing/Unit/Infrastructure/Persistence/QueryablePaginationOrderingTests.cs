using Archon.Core.Entities;
using Archon.Core.Pagination;
using Archon.Infrastructure.Persistence.EF;
using Microsoft.EntityFrameworkCore;

namespace Archon.Testing.Unit.Infrastructure.Persistence
{
    /// <summary>
    /// `Skip`/`Take` sem `ORDER BY` nao tem ordem garantida pelo banco: o mesmo registro pode aparecer
    /// em duas paginas e outro sumir. Como este e o helper de paginacao padrao, listagem que esquecesse
    /// o `OrderBy` herdava isso em silencio.
    /// </summary>
    public sealed class QueryablePaginationOrderingTests
    {
        private static OrderingDbContext CreateContext(params int[] values)
        {
            DbContextOptions<OrderingDbContext> options = new DbContextOptionsBuilder<OrderingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            OrderingDbContext context = new(options);

            foreach (int value in values)
            {
                context.Items.Add(new OrderedEntity { Value = value });
            }

            context.SaveChanges();
            context.ChangeTracker.Clear();

            return context;
        }

        [Test]
        public async Task ToPagedResultAsync_ShouldApplyDeterministicOrder_WhenQueryIsNotOrdered()
        {
            using OrderingDbContext context = CreateContext(50, 40, 30, 20, 10);
            PagedRequest request = new() { Page = 1, PageSize = 3 };

            PagedResult<OrderedEntity> result = await context.Items.AsQueryable().ToPagedResultAsync(request);

            // Sem ordenacao explicita, o fallback ordena por Id — criterio arbitrario, mas estavel.
            Assert.That(result.Items.Select(item => item.Id), Is.Ordered);
        }

        [Test]
        public async Task ToPagedResultAsync_ShouldKeepCallerOrdering_WhenQueryIsAlreadyOrdered()
        {
            using OrderingDbContext context = CreateContext(10, 20, 30, 40, 50);
            PagedRequest request = new() { Page = 1, PageSize = 3 };

            PagedResult<OrderedEntity> result = await context.Items
                .OrderByDescending(item => item.Value)
                .ToPagedResultAsync(request);

            Assert.That(result.Items.First().Value, Is.EqualTo(50));
            Assert.That(result.Items.Select(item => item.Value), Is.Ordered.Descending);
        }

        [Test]
        public async Task ToPagedResultAsync_ShouldKeepCallerOrdering_WhenWhereComesAfterOrderBy()
        {
            using OrderingDbContext context = CreateContext(10, 20, 30, 40, 50);
            PagedRequest request = new() { Page = 1, PageSize = 5 };

            // Caso que derruba a deteccao ingenua: OrderBy(...).Where(...) volta como IQueryable, nao
            // como IOrderedQueryable. Checar o tipo faria o helper achar que nao ha ordenacao e
            // sobrescrever a ordem pedida por quem chamou. Por isso a deteccao percorre a expressao.
            PagedResult<OrderedEntity> result = await context.Items
                .OrderByDescending(item => item.Value)
                .Where(item => item.Value > 10)
                .ToPagedResultAsync(request);

            Assert.That(result.Items.First().Value, Is.EqualTo(50));
            Assert.That(result.Items.Select(item => item.Value), Is.Ordered.Descending);
        }

        private sealed class OrderedEntity : Entity
        {
            public int Value { get; set; }
        }

        private sealed class OrderingDbContext : DbContext
        {
            public OrderingDbContext(DbContextOptions<OrderingDbContext> options) : base(options)
            {
            }

            public DbSet<OrderedEntity> Items => Set<OrderedEntity>();
        }
    }
}
