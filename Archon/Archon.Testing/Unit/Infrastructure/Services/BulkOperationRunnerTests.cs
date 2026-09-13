using Archon.Core.Bulk;
using Archon.Core.Entities;
using Archon.Core.Exceptions;
using Archon.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Archon.Testing.Unit.Infrastructure.Services
{
    public sealed class BulkOperationRunnerTests
    {
        private SqliteConnection connection = null!;

        [SetUp]
        public void SetUp()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
        }

        [TearDown]
        public void TearDown()
        {
            connection.Dispose();
        }

        private TestDbContext CreateContext()
        {
            DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(connection)
                .Options;

            TestDbContext context = new(options);
            context.Database.EnsureCreated();
            return context;
        }

        private static BulkOperationRunner CreateRunner(DbContext context)
        {
            return new BulkOperationRunner(context, NullLogger<BulkOperationRunner>.Instance);
        }

        private static async Task<long[]> SeedAsync(TestDbContext context, params (string Name, bool IsActive, bool Locked)[] items)
        {
            List<ToggleEntity> entities = items
                .Select(item => new ToggleEntity { Name = item.Name, IsActiveValue = item.IsActive, Locked = item.Locked })
                .ToList();
            context.Items.AddRange(entities);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return entities.Select(entity => entity.Id).ToArray();
        }

        [Test]
        public async Task RunAsync_ShouldProcessEachIdOnce_InReceivedOrder()
        {
            using TestDbContext context = CreateContext();
            List<long> calls = [];

            BulkOperationResult result = await CreateRunner(context).RunAsync([3, 1, 3, 2], (id, _) =>
            {
                calls.Add(id);
                return Task.CompletedTask;
            });

            Assert.That(calls, Is.EqualTo(new long[] { 3, 1, 2 }));
            Assert.That(result.Total, Is.EqualTo(3));
            Assert.That(result.Succeeded, Is.EqualTo(3));
            Assert.That(result.SucceededIds, Is.EqualTo(new long[] { 3, 1, 2 }));
        }

        [Test]
        public async Task RunAsync_ShouldIsolateFailures_AndKeepTheRawKeyWithArgs()
        {
            using TestDbContext context = CreateContext();

            BulkOperationResult result = await CreateRunner(context).RunAsync([1, 2, 3], (id, _) => id switch
            {
                2 => throw new BusinessRuleException("item.locked", "Beta"),
                _ => Task.CompletedTask
            });

            Assert.That(result.Succeeded, Is.EqualTo(2));
            Assert.That(result.Failed, Is.EqualTo(1));
            BulkOperationFailure failure = result.Failures.Single();
            Assert.That(failure.Id, Is.EqualTo(2));
            Assert.That(failure.MessageKey, Is.EqualTo("item.locked"));
            Assert.That(failure.MessageArgs, Is.EqualTo(new object[] { "Beta" }));
        }

        [Test]
        public async Task RunAsync_ShouldMapUnexpectedExceptions_ToTheGenericKey()
        {
            using TestDbContext context = CreateContext();

            BulkOperationResult result = await CreateRunner(context).RunAsync([1], (_, _) => throw new NullReferenceException());

            Assert.That(result.Failures.Single().MessageKey, Is.EqualTo("error.unexpected.short"));
        }

        [Test]
        public async Task RunAsync_ShouldClearPendingChanges_SoOneFailureDoesNotLeakIntoTheNextSave()
        {
            using TestDbContext context = CreateContext();
            long[] ids = await SeedAsync(context, ("A", true, false), ("B", true, false));

            // O primeiro registro altera a entidade e falha antes de gravar. Sem limpar o rastreador, o
            // SaveChanges do segundo gravaria tambem a alteracao do primeiro.
            BulkOperationResult result = await CreateRunner(context).RunAsync(ids, async (id, token) =>
            {
                ToggleEntity entity = await context.Items.AsTracking().SingleAsync(item => item.Id == id, token);
                entity.Name = $"{entity.Name}-changed";

                if (id == ids[0])
                {
                    throw new BusinessRuleException("item.locked");
                }

                await context.SaveChangesAsync(token);
            });

            Assert.That(result.Succeeded, Is.EqualTo(1));
            context.ChangeTracker.Clear();
            string[] names = await context.Items.OrderBy(item => item.Id).Select(item => item.Name).ToArrayAsync();
            Assert.That(names, Is.EqualTo(new[] { "A", "B-changed" }));
        }

        [Test]
        public void RunAsync_ShouldStop_WhenCancelled()
        {
            using TestDbContext context = CreateContext();
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await CreateRunner(context).RunAsync([1, 2], (_, _) => Task.CompletedTask, cancellation.Token));
        }

        [Test]
        public async Task SetActiveAsync_ShouldToggleOnlyTheIsActiveFlag()
        {
            using TestDbContext context = CreateContext();
            long[] ids = await SeedAsync(context, ("A", true, false), ("B", true, false));

            BulkOperationResult result = await CreateRunner(context).SetActiveAsync<ToggleEntity>(ids, isActive: false);

            Assert.That(result.Succeeded, Is.EqualTo(2));
            context.ChangeTracker.Clear();
            List<ToggleEntity> stored = await context.Items.OrderBy(item => item.Id).ToListAsync();
            Assert.That(stored.Select(item => item.IsActiveValue), Is.All.False);
            Assert.That(stored.Select(item => item.Name), Is.EqualTo(new[] { "A", "B" }));
        }

        [Test]
        public async Task SetActiveAsync_ShouldCountAlreadyInTargetState_AsSuccessWithoutCallingTheEntity()
        {
            using TestDbContext context = CreateContext();
            long[] ids = await SeedAsync(context, ("Locked but active", true, true));

            // Activate lancaria por estar travado; como ja esta ativo, nada e chamado.
            BulkOperationResult result = await CreateRunner(context).SetActiveAsync<ToggleEntity>(ids, isActive: true);

            Assert.That(result.Succeeded, Is.EqualTo(1));
            Assert.That(result.Failed, Is.EqualTo(0));
        }

        [Test]
        public async Task SetActiveAsync_ShouldReportNotFound_AndDomainRules()
        {
            using TestDbContext context = CreateContext();
            long[] ids = await SeedAsync(context, ("Free", true, false), ("Locked", true, true));

            BulkOperationResult result = await CreateRunner(context).SetActiveAsync<ToggleEntity>([ids[0], ids[1], 999], isActive: false);

            Assert.That(result.SucceededIds, Is.EqualTo(new[] { ids[0] }));
            Assert.That(result.Failures.Select(failure => (failure.Id, failure.MessageKey)), Is.EquivalentTo(new[]
            {
                (ids[1], "item.locked"),
                (999L, "record.notFound")
            }));
        }

        [Test]
        public async Task SetActiveAsync_ShouldRunTheValidator_BeforeChangingTheEntity()
        {
            using TestDbContext context = CreateContext();
            long[] ids = await SeedAsync(context, ("A", false, false), ("B", false, false));

            BulkOperationResult result = await CreateRunner(context).SetActiveAsync<ToggleEntity>(ids, isActive: true, validate: async (entity, token) =>
            {
                bool anotherActive = await context.Items.AnyAsync(item => item.IsActiveValue && item.Id != entity.Id, token);
                if (anotherActive)
                {
                    throw new ConflictException("item.onlyOneActive");
                }
            });

            Assert.That(result.SucceededIds, Is.EqualTo(new[] { ids[0] }));
            Assert.That(result.Failures.Single().MessageKey, Is.EqualTo("item.onlyOneActive"));
            context.ChangeTracker.Clear();
            Assert.That(await context.Items.CountAsync(item => item.IsActiveValue), Is.EqualTo(1));
        }

        private sealed class ToggleEntity : Entity, IActivatable
        {
            public string Name { get; set; } = string.Empty;

            public bool IsActiveValue { get; set; }

            public bool Locked { get; set; }

            public bool IsActive => IsActiveValue;

            public void Activate()
            {
                EnsureUnlocked();
                IsActiveValue = true;
            }

            public void Deactivate()
            {
                EnsureUnlocked();
                IsActiveValue = false;
            }

            private void EnsureUnlocked()
            {
                if (Locked)
                {
                    throw new BusinessRuleException("item.locked");
                }
            }
        }

        private sealed class TestDbContext : DbContext
        {
            public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
            {
            }

            public DbSet<ToggleEntity> Items => Set<ToggleEntity>();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<ToggleEntity>().HasKey(item => item.Id);
                modelBuilder.Entity<ToggleEntity>().Property(item => item.Id).ValueGeneratedOnAdd();
                modelBuilder.Entity<ToggleEntity>().Ignore(item => item.IsActive);
            }
        }
    }
}
