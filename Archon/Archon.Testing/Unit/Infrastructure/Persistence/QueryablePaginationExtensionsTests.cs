using Archon.Core.Pagination;
using Archon.Infrastructure.Persistence.EF;
using Microsoft.EntityFrameworkCore;

namespace Archon.Testing.Unit.Infrastructure.Persistence
{
    public sealed class QueryablePaginationExtensionsTests
    {
        private TestDbContext CreateContext(int count)
        {
            DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            TestDbContext context = new TestDbContext(options);
            for (int i = 1; i <= count; i++)
            {
                context.Items.Add(new PaginationItem { Value = i });
            }
            context.SaveChanges();
            return context;
        }

        [Test]
        public async Task ToPagedResultAsync_ShouldReturnCorrectPage()
        {
            using TestDbContext context = CreateContext(100);
            PagedRequest request = new PagedRequest { Page = 2, PageSize = 20 };

            PagedResult<PaginationItem> result = await context.Items.AsQueryable().ToPagedResultAsync(request);

            Assert.That(result.Items.Count, Is.EqualTo(20));
            Assert.That(result.Items.First().Value, Is.EqualTo(21));
            Assert.That(result.Items.Last().Value, Is.EqualTo(40));
            Assert.That(result.Pagination.Page, Is.EqualTo(2));
            Assert.That(result.Pagination.TotalCount, Is.EqualTo(100));
            Assert.That(result.Pagination.TotalPages, Is.EqualTo(5));
            Assert.That(result.Pagination.HasPreviousPage, Is.True);
            Assert.That(result.Pagination.HasNextPage, Is.True);
        }

        [Test]
        public async Task ToPagedResultAsync_ShouldReturnFirstPage()
        {
            using TestDbContext context = CreateContext(50);
            PagedRequest request = new PagedRequest { Page = 1, PageSize = 20 };

            PagedResult<PaginationItem> result = await context.Items.AsQueryable().ToPagedResultAsync(request);

            Assert.That(result.Items.Count, Is.EqualTo(20));
            Assert.That(result.Pagination.HasPreviousPage, Is.False);
            Assert.That(result.Pagination.HasNextPage, Is.True);
        }

        [Test]
        public async Task ToPagedResultAsync_ShouldReturnLastPage()
        {
            using TestDbContext context = CreateContext(50);
            PagedRequest request = new PagedRequest { Page = 3, PageSize = 20 };

            PagedResult<PaginationItem> result = await context.Items.AsQueryable().ToPagedResultAsync(request);

            Assert.That(result.Items.Count, Is.EqualTo(10));
            Assert.That(result.Pagination.HasNextPage, Is.False);
            Assert.That(result.Pagination.HasPreviousPage, Is.True);
        }

        [Test]
        public async Task ToPagedResultAsync_ShouldReturnEmpty_WhenNoItems()
        {
            using TestDbContext context = CreateContext(0);
            PagedRequest request = new PagedRequest();

            PagedResult<PaginationItem> result = await context.Items.AsQueryable().ToPagedResultAsync(request);

            Assert.That(result.Items.Count, Is.EqualTo(0));
            Assert.That(result.Pagination.TotalCount, Is.EqualTo(0));
            Assert.That(result.Pagination.TotalPages, Is.EqualTo(0));
            Assert.That(result.Pagination.HasPreviousPage, Is.False);
            Assert.That(result.Pagination.HasNextPage, Is.False);
        }

        [Test]
        public async Task ToPagedResultAsync_ShouldThrow_WhenQueryIsNull()
        {
            IQueryable<PaginationItem>? query = null;
            PagedRequest request = new PagedRequest();

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await query!.ToPagedResultAsync(request);
            });
        }

        [Test]
        public async Task ToPagedResultAsync_ShouldThrow_WhenRequestIsNull()
        {
            using TestDbContext context = CreateContext(10);
            PagedRequest? request = null;

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await context.Items.AsQueryable().ToPagedResultAsync(request!);
            });
        }

        [Test]
        public async Task ToPagedResultAsync_WithSelector_ShouldProjectItems()
        {
            using TestDbContext context = CreateContext(50);
            PagedRequest request = new PagedRequest { Page = 1, PageSize = 10 };

            PagedResult<string> result = await context.Items.AsQueryable().ToPagedResultAsync(request, i => $"Item {i.Value}");

            Assert.That(result.Items.Count, Is.EqualTo(10));
            Assert.That(result.Items.First(), Is.EqualTo("Item 1"));
            Assert.That(result.Pagination.TotalCount, Is.EqualTo(50));
        }

        [Test]
        public async Task ToPagedResultAsync_WithSelector_ShouldThrow_WhenSelectorIsNull()
        {
            using TestDbContext context = CreateContext(10);
            PagedRequest request = new PagedRequest();
            Func<PaginationItem, string>? selector = null;

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await context.Items.AsQueryable().ToPagedResultAsync(request, selector!);
            });
        }

        private class PaginationItem
        {
            public int Id { get; set; }
            public int Value { get; set; }
        }

        private class TestDbContext : DbContext
        {
            public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
            public DbSet<PaginationItem> Items => Set<PaginationItem>();
        }
    }
}
