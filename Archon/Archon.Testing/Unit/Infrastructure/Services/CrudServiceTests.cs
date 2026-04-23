using Archon.Application.Abstractions;
using Archon.Application.MultiTenancy;
using Archon.Core.Entities;
using Archon.Core.Exceptions;
using Archon.Core.ValueObjects;
using Archon.Infrastructure.Persistence.EF;
using Archon.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Archon.Testing.Unit.Infrastructure.Services
{
    public sealed class CrudServiceTests
    {
        private TestDbContext CreateContext()
        {
            DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new TestDbContext(options);
        }

        [Test]
        public async Task Insert_ShouldAddEntity()
        {
            using TestDbContext context = CreateContext();
            TestCrudService service = new(context);
            TestEntity entity = new TestEntity { Name = "Test" };

            bool result = await service.Insert(CancellationToken.None, entity);

            Assert.That(result, Is.True);
            Assert.That(entity.Id, Is.GreaterThan(0));
            Assert.That(entity.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public async Task Insert_ShouldReturnTrue_WhenNoEntities()
        {
            using TestDbContext context = CreateContext();
            TestCrudService service = new(context);

            bool result = await service.Insert(CancellationToken.None);

            Assert.That(result, Is.True);
        }

        [Test]
        public async Task Insert_ShouldSetCreatedAt()
        {
            using TestDbContext context = CreateContext();
            TestCrudService service = new(context);
            TestEntity entity = new TestEntity { Name = "Test" };

            await service.Insert(CancellationToken.None, entity);

            Assert.That(entity.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
            Assert.That(entity.UpdatedAt, Is.EqualTo(entity.CreatedAt));
        }

        [Test]
        public async Task Insert_ShouldFail_WhenValidationFails()
        {
            using TestDbContext context = CreateContext();
            FailingValidationService service = new(context);
            TestEntity entity = new TestEntity { Name = "Test" };

            bool result = await service.Insert(CancellationToken.None, entity);

            Assert.That(result, Is.False);
            Assert.That(service.Messages.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task Update_ShouldModifyEntity()
        {
            using TestDbContext context = CreateContext();
            TestEntity entity = new TestEntity { Name = "Original" };
            context.Entities.Add(entity);
            await context.SaveChangesAsync();

            TestCrudService service = new(context);
            TestEntity updatedEntity = new TestEntity { Name = "Updated" };
            typeof(Entity).GetProperty("Id")!.SetValue(updatedEntity, entity.Id);

            TestEntity? result = await service.Update(updatedEntity);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Name, Is.EqualTo("Updated"));
            Assert.That(result.UpdatedAt, Is.Not.Null);
        }

        [Test]
        public async Task Update_ShouldReturnNull_WhenEntityNotFound()
        {
            using TestDbContext context = CreateContext();
            TestCrudService service = new(context);
            TestEntity entity = new TestEntity { Name = "Test" };
            typeof(Entity).GetProperty("Id")!.SetValue(entity, 999L);

            TestEntity? result = await service.Update(entity);

            Assert.That(result, Is.Null);
            Assert.That(service.Messages.Any(m => m is KeyNotFoundException), Is.True);
        }

        [Test]
        public async Task Delete_ById_ShouldRemoveEntity()
        {
            using TestDbContext context = CreateContext();
            TestEntity entity = new TestEntity { Name = "ToDelete" };
            context.Entities.Add(entity);
            await context.SaveChangesAsync();

            TestCrudService service = new(context);
            TestEntity? result = await service.Delete(entity.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(await context.Entities.AnyAsync(e => e.Id == entity.Id), Is.False);
        }

        [Test]
        public async Task Delete_ById_ShouldReturnNull_WhenNotFound()
        {
            using TestDbContext context = CreateContext();
            TestCrudService service = new(context);

            TestEntity? result = await service.Delete(999);

            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task Delete_ByEntities_ShouldRemoveMultiple()
        {
            using TestDbContext context = CreateContext();
            TestEntity e1 = new TestEntity { Name = "A" };
            TestEntity e2 = new TestEntity { Name = "B" };
            context.Entities.AddRange(e1, e2);
            await context.SaveChangesAsync();

            TestCrudService service = new(context);
            bool result = await service.Delete([e1, e2]);

            Assert.That(result, Is.True);
            Assert.That(await context.Entities.CountAsync(), Is.EqualTo(0));
        }

        [Test]
        public async Task ExecuteInTransaction_ShouldRollback_OnFailure()
        {
            using TestDbContext context = CreateContext();
            TestCrudService service = new(context);

            bool result = await service.ExecuteInTransaction(async () =>
            {
                context.Entities.Add(new TestEntity { Name = "A" });
                throw new InvalidOperationException("Simulated error");
            });

            Assert.That(result, Is.False);
            Assert.That(await context.Entities.CountAsync(), Is.EqualTo(0));
        }

        [Test]
        public void ExecuteInTransaction_ShouldThrowIntegrityException_OnDbUpdateException()
        {
            using TestDbContext context = CreateContext();
            TestCrudService service = new(context);

            Assert.ThrowsAsync<IntegrityException>(async () =>
            {
                await service.ExecuteInTransaction(async () =>
                {
                    throw new DbUpdateException("constraint violation");
                });
            });
        }

        [Test]
        public void Validate_ShouldReturnFalse_WhenEntityIsNull()
        {
            using TestDbContext context = CreateContext();
            TestCrudService service = new(context);

            bool result = service.Validate(null!);

            Assert.That(result, Is.False);
            Assert.That(service.Messages.Any(m => m is ArgumentNullException), Is.True);
        }

        [Test]
        public void Validate_ShouldUseDataAnnotations()
        {
            using TestDbContext context = CreateContext();
            TestCrudService service = new(context);
            TestEntity entity = new TestEntity { Name = "" };

            bool result = service.Validate(entity);

            Assert.That(result, Is.False);
        }

        [Test]
        public void CustomValidate_ShouldBeCalled()
        {
            using TestDbContext context = CreateContext();
            CustomValidationService service = new(context);
            TestEntity entity = new TestEntity { Name = "Invalid" };

            bool result = service.Validate(entity);

            Assert.That(result, Is.False);
            Assert.That(service.CustomValidateCalled, Is.True);
        }

        [Test]
        public void GetErrorMessages_ShouldConcatenateMessages()
        {
            using TestDbContext context = CreateContext();
            TestCrudService service = new(context);
            AddMessage(service, new Exception("Error 1"));
            AddMessage(service, new Exception("Error 2"));

            string result = service.GetErrorMessages();

            Assert.That(result, Does.Contain("Error 1"));
            Assert.That(result, Does.Contain("Error 2"));
        }

        private static void AddMessage<T>(CrudService<T> service, Exception exception) where T : Entity
        {
            System.Reflection.PropertyInfo? mutableMessagesProperty = typeof(CrudService<T>).GetProperty("MutableMessages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            object? mutableMessages = mutableMessagesProperty?.GetValue(service);
            mutableMessages?.GetType().GetMethod("Add")?.Invoke(mutableMessages, [exception]);
        }

        private class TestEntity : Entity
        {
            [System.ComponentModel.DataAnnotations.Required]
            public string Name { get; set; } = string.Empty;
        }

        private class TestDbContext : DbContext
        {
            public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
            public DbSet<TestEntity> Entities => Set<TestEntity>();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestEntity>().HasKey(e => e.Id);
            }
        }

        private class TestCrudService : CrudService<TestEntity>
        {
            public TestCrudService(DbContext dbContext) : base(dbContext) { }
        }

        private class FailingValidationService : CrudService<TestEntity>
        {
            public FailingValidationService(DbContext dbContext) : base(dbContext) { }
            public override bool CustomValidate(TestEntity entity)
            {
                MutableMessages.Add(new Exception("Custom validation failed"));
                return false;
            }
        }

        private class CustomValidationService : CrudService<TestEntity>
        {
            public CustomValidationService(DbContext dbContext) : base(dbContext) { }
            public bool CustomValidateCalled { get; private set; }

            public override bool CustomValidate(TestEntity entity)
            {
                CustomValidateCalled = true;
                if (entity.Name == "Invalid")
                {
                    MutableMessages.Add(new Exception("Invalid name"));
                    return false;
                }
                return true;
            }
        }
    }
}
