using Archon.Core.Entities;
using Archon.Core.Events;
using Archon.Application.Abstractions;
using Archon.Application.Events;
using Archon.Application.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Archon.Infrastructure.Persistence.EF
{
    public class ArchonDbContext : DbContext
    {
        private readonly IReadOnlyCollection<Assembly> modelAssemblies;
        private readonly ArchonAuditManager auditManager;
        private readonly IDomainEventDispatcher? domainEventDispatcher;
        private readonly string? schema;
        private bool isAuditing;

        public ArchonDbContext(
            DbContextOptions<ArchonDbContext> options,
            ModelAssemblyRegistry modelAssemblyRegistry,
            ICurrentUser? currentUser = null,
            ITenantContext? tenantContext = null,
            IDomainEventDispatcher? domainEventDispatcher = null,
            string? schema = null) : base(options)
        {
            modelAssemblies = modelAssemblyRegistry.Assemblies;
            auditManager = new ArchonAuditManager(ChangeTracker, currentUser, tenantContext);
            this.domainEventDispatcher = domainEventDispatcher;
            this.schema = schema;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (!string.IsNullOrWhiteSpace(schema))
            {
                modelBuilder.HasDefaultSchema(schema);
            }

            List<Type> entityTypes = modelAssemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type =>
                    type.IsClass &&
                    !type.IsAbstract &&
                    typeof(Entity).IsAssignableFrom(type))
                .ToList();

            foreach (Type entityType in entityTypes)
            {
                modelBuilder.Entity(entityType);
            }

            ArchonModelConventions.Apply(modelBuilder);

            foreach (Assembly assembly in modelAssemblies)
            {
                modelBuilder.ApplyConfigurationsFromAssembly(assembly);
            }

            ArchonModelConventions.ApplyIdentifierConventions(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            return SaveChangesAsync(acceptAllChangesOnSuccess, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override int SaveChanges()
        {
            return SaveChanges(true);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return SaveChangesAsync(true, cancellationToken);
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            if (isAuditing)
            {
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }

            List<IDomainEvent> domainEvents = CollectDomainEvents();
            auditManager.ApplyEntityTimestamps();
            List<AuditEntry> auditEntries = auditManager.CreateAuditEntries();
            int result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            if (auditEntries.Count > 0)
            {
                await PersistAuditEntriesAsync(auditEntries, cancellationToken);
            }

            if (domainEvents.Count > 0 && domainEventDispatcher is not null)
            {
                await domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);
            }

            return result;
        }

        private List<IDomainEvent> CollectDomainEvents()
        {
            List<IDomainEvent> domainEvents = ChangeTracker
                .Entries<Entity>()
                .SelectMany(entry => entry.Entity.DomainEvents)
                .ToList();

            ChangeTracker
                .Entries<Entity>()
                .ToList()
                .ForEach(entry => entry.Entity.ClearDomainEvents());

            return domainEvents;
        }

        private async Task PersistAuditEntriesAsync(IReadOnlyCollection<AuditEntry> auditEntries, CancellationToken cancellationToken)
        {
            isAuditing = true;

            try
            {
                Set<AuditEntry>().AddRange(auditEntries);
                await base.SaveChangesAsync(true, cancellationToken);
            }
            finally
            {
                isAuditing = false;
            }
        }
    }
}
