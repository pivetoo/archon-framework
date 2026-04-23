using Archon.Application.Abstractions;
using Archon.Application.MultiTenancy;
using Archon.Core.Entities;
using Archon.Core.ValueObjects;
using Archon.Infrastructure.Persistence.EF;
using Microsoft.EntityFrameworkCore;

namespace Archon.Testing.Unit.Infrastructure.Persistence
{
    public sealed class ArchonAuditManagerTests
    {
        private TestDbContext CreateContext(ICurrentUser? currentUser = null, ITenantContext? tenantContext = null)
        {
            DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new TestDbContext(options, currentUser, tenantContext);
        }

        private static void SetId(Entity entity, long id)
        {
            typeof(Entity).GetProperty("Id")!.SetValue(entity, id);
        }

        [Test]
        public void ApplyEntityTimestamps_ShouldSetCreatedAt_ForAddedEntities()
        {
            using TestDbContext context = CreateContext();
            TestAuditableEntity entity = new TestAuditableEntity { Name = "Test" };
            context.Entities.Add(entity);

            ArchonAuditManager auditManager = new(context.ChangeTracker, null, null);
            auditManager.ApplyEntityTimestamps();

            Assert.That(entity.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
            Assert.That(entity.UpdatedAt, Is.EqualTo(entity.CreatedAt));
        }

        [Test]
        public void ApplyEntityTimestamps_ShouldSetUpdatedAt_ForModifiedEntities()
        {
            using TestDbContext context = CreateContext();
            TestAuditableEntity entity = new TestAuditableEntity { Name = "Original" };
            SetId(entity, 1);
            context.Entities.Attach(entity);
            context.Entry(entity).State = EntityState.Modified;

            ArchonAuditManager auditManager = new(context.ChangeTracker, null, null);
            auditManager.ApplyEntityTimestamps();

            Assert.That(entity.UpdatedAt, Is.Not.Null);
            Assert.That(entity.UpdatedAt, Is.GreaterThan(default(DateTimeOffset)));
        }

        [Test]
        public void ApplyEntityTimestamps_ShouldIgnoreAuditEntities()
        {
            using TestDbContext context = CreateContext();
            DateTimeOffset originalCreatedAt = DateTimeOffset.UtcNow.AddHours(-1);
            AuditEntry auditEntry = new AuditEntry("Test", "1", AuditAction.Insert, DateTimeOffset.UtcNow);
            typeof(Entity).GetProperty("CreatedAt")!.SetValue(auditEntry, originalCreatedAt);
            typeof(Entity).GetProperty("UpdatedAt")!.SetValue(auditEntry, originalCreatedAt);
            context.AuditEntries.Add(auditEntry);

            ArchonAuditManager auditManager = new(context.ChangeTracker, null, null);
            auditManager.ApplyEntityTimestamps();

            Assert.That(auditEntry.CreatedAt, Is.EqualTo(originalCreatedAt));
        }

        [Test]
        public void CreateAuditEntries_ShouldCaptureInsert()
        {
            using TestDbContext context = CreateContext();
            TestAuditableEntity entity = new TestAuditableEntity { Name = "Test" };
            SetId(entity, 1);
            context.Entities.Add(entity);

            ArchonAuditManager auditManager = new(context.ChangeTracker, null, null);
            auditManager.ApplyEntityTimestamps();
            List<AuditEntry> entries = auditManager.CreateAuditEntries();

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].Action, Is.EqualTo(AuditAction.Insert));
            Assert.That(entries[0].EntityName, Is.EqualTo(nameof(TestAuditableEntity)));
            Assert.That(entries[0].EntityId, Is.EqualTo("1"));
            Assert.That(entries[0].PropertyChanges.Count, Is.GreaterThan(0));
        }

        [Test]
        public void CreateAuditEntries_ShouldCaptureUpdate_WithPropertyChanges()
        {
            using TestDbContext context = CreateContext();
            TestAuditableEntity entity = new TestAuditableEntity { Name = "Original" };
            context.Entities.Add(entity);
            context.SaveChanges();

            TestAuditableEntity tracked = context.Entities.First();
            tracked.Name = "Updated";

            ArchonAuditManager auditManager = new(context.ChangeTracker, null, null);
            auditManager.ApplyEntityTimestamps();
            List<AuditEntry> entries = auditManager.CreateAuditEntries();

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].Action, Is.EqualTo(AuditAction.Update));
            Assert.That(entries[0].PropertyChanges.Any(pc => pc.PropertyName == nameof(TestAuditableEntity.Name)), Is.True);
        }

        [Test]
        public void CreateAuditEntries_ShouldCaptureDelete()
        {
            using TestDbContext context = CreateContext();
            TestAuditableEntity entity = new TestAuditableEntity { Name = "ToDelete" };
            SetId(entity, 1);
            context.Entities.Attach(entity);
            context.Entry(entity).State = EntityState.Deleted;

            ArchonAuditManager auditManager = new(context.ChangeTracker, null, null);
            List<AuditEntry> entries = auditManager.CreateAuditEntries();

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].Action, Is.EqualTo(AuditAction.Delete));
        }

        [Test]
        public void CreateAuditEntries_ShouldSetChangedBy_FromCurrentUser()
        {
            MockCurrentUser currentUser = new MockCurrentUser { Email = "test@archon.dev" };
            using TestDbContext context = CreateContext(currentUser);
            TestAuditableEntity entity = new TestAuditableEntity { Name = "Test" };
            SetId(entity, 1);
            context.Entities.Add(entity);

            ArchonAuditManager auditManager = new(context.ChangeTracker, currentUser, null);
            auditManager.ApplyEntityTimestamps();
            List<AuditEntry> entries = auditManager.CreateAuditEntries();

            Assert.That(entries[0].ChangedBy, Is.EqualTo("test@archon.dev"));
        }

        [Test]
        public void CreateAuditEntries_ShouldSetTenantId_FromTenantContext()
        {
            MockTenantContext tenantContext = new MockTenantContext { TenantId = "tenant-a" };
            using TestDbContext context = CreateContext(tenantContext: tenantContext);
            TestAuditableEntity entity = new TestAuditableEntity { Name = "Test" };
            SetId(entity, 1);
            context.Entities.Add(entity);

            ArchonAuditManager auditManager = new(context.ChangeTracker, null, tenantContext);
            auditManager.ApplyEntityTimestamps();
            List<AuditEntry> entries = auditManager.CreateAuditEntries();

            Assert.That(entries[0].TenantId, Is.EqualTo("tenant-a"));
        }

        [Test]
        public void CreateAuditEntries_ShouldSetCorrelationId_FromActivity()
        {
            using TestDbContext context = CreateContext();
            TestAuditableEntity entity = new TestAuditableEntity { Name = "Test" };
            SetId(entity, 1);
            context.Entities.Add(entity);

            using System.Diagnostics.Activity activity = new System.Diagnostics.Activity("TestActivity");
            activity.Start();

            ArchonAuditManager auditManager = new(context.ChangeTracker, null, null);
            auditManager.ApplyEntityTimestamps();
            List<AuditEntry> entries = auditManager.CreateAuditEntries();

            Assert.That(entries[0].CorrelationId, Is.EqualTo(activity.TraceId.ToString()));
        }

        [Test]
        public void CreateAuditEntries_ShouldNotIncludeUnmodifiedProperties()
        {
            using TestDbContext context = CreateContext();
            TestAuditableEntity entity = new TestAuditableEntity { Name = "Test", Description = "Desc" };
            context.Entities.Add(entity);
            context.SaveChanges();

            TestAuditableEntity tracked = context.Entities.First();
            tracked.Name = "Updated";

            ArchonAuditManager auditManager = new(context.ChangeTracker, null, null);
            auditManager.ApplyEntityTimestamps();
            List<AuditEntry> entries = auditManager.CreateAuditEntries();

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].PropertyChanges.Any(pc => pc.PropertyName == nameof(TestAuditableEntity.Name)), Is.True);
            Assert.That(entries[0].PropertyChanges.Any(pc => pc.PropertyName == nameof(TestAuditableEntity.Description)), Is.False);
        }

        private class TestAuditableEntity : Entity
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        private class TestDbContext : DbContext
        {
            private readonly ICurrentUser? currentUser;
            private readonly ITenantContext? tenantContext;

            public TestDbContext(DbContextOptions<TestDbContext> options, ICurrentUser? currentUser = null, ITenantContext? tenantContext = null) : base(options)
            {
                this.currentUser = currentUser;
                this.tenantContext = tenantContext;
            }

            public DbSet<TestAuditableEntity> Entities => Set<TestAuditableEntity>();
            public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<TestAuditableEntity>().HasKey(e => e.Id);
                modelBuilder.Entity<AuditEntry>().HasKey(e => e.Id);
                modelBuilder.Entity<AuditPropertyChange>().HasKey(e => e.Id);
            }
        }

        private class MockCurrentUser : ICurrentUser
        {
            public bool IsAuthenticated => true;
            public long? UserId => 1;
            public string? UserName => "Test User";
            public string? Email { get; set; }
            public string? ClientId => "test-client";
        }

        private class MockTenantContext : ITenantContext
        {
            public string? TenantId { get; set; }
            public string? CompanyName => "Test";
            public string? ApplicationId => "test";
            public string? ConnectionString => "inmemory";
            public string? Schema => null;
            public DatabaseProvider DatabaseProvider => DatabaseProvider.PostgreSql;
            public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
        }
    }
}
