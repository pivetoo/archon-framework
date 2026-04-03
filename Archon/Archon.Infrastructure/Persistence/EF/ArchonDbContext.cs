using Archon.Core.Entities;
using Archon.Application.Abstractions;
using Archon.Application.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Archon.Infrastructure.Persistence.EF
{
    public class ArchonDbContext : DbContext
    {
        private readonly IReadOnlyCollection<Assembly> modelAssemblies;
        private readonly ArchonAuditManager auditManager;
        private readonly string? schema;
        private bool isAuditing;

        public ArchonDbContext(DbContextOptions<ArchonDbContext> options, ModelAssemblyRegistry modelAssemblyRegistry, ICurrentUser? currentUser = null, ITenantContext? tenantContext = null, string? schema = null) : base(options)
        {
            modelAssemblies = modelAssemblyRegistry.Assemblies;
            auditManager = new ArchonAuditManager(ChangeTracker, currentUser, tenantContext);
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

            auditManager.ApplyEntityTimestamps();
            List<AuditEntry> auditEntries = auditManager.CreateAuditEntries();
            int result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            if (auditEntries.Count == 0)
            {
                return result;
            }

            await PersistAuditEntriesAsync(auditEntries, cancellationToken);
            return result;
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
