using System.Diagnostics;
using Archon.Core.Entities;
using Archon.Core.ValueObjects;
using Archon.Application.Abstractions;
using Archon.Application.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;

namespace Archon.Infrastructure.Persistence.EF
{
    public class ArchonDbContext : DbContext
    {
        private readonly IReadOnlyCollection<Assembly> modelAssemblies;
        private readonly ICurrentUser? currentUser;
        private readonly ITenantContext? tenantContext;
        private readonly string? schema;
        private bool isAuditing;

        public ArchonDbContext(DbContextOptions<ArchonDbContext> options, ModelAssemblyRegistry modelAssemblyRegistry, ICurrentUser? currentUser = null, ITenantContext? tenantContext = null, string? schema = null) : base(options)
        {
            modelAssemblies = modelAssemblyRegistry.Assemblies;
            this.currentUser = currentUser;
            this.tenantContext = tenantContext;
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

            foreach (Assembly assembly in modelAssemblies)
            {
                modelBuilder.ApplyConfigurationsFromAssembly(assembly);
            }

            ApplyIdentityKeyConventions(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }

        private static void ApplyIdentityKeyConventions(ModelBuilder modelBuilder)
        {
            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (!typeof(Entity).IsAssignableFrom(entityType.ClrType))
                {
                    continue;
                }

                IMutableProperty? idProperty = entityType.FindProperty(nameof(Entity.Id));
                if (idProperty is null)
                {
                    continue;
                }

                idProperty.ValueGenerated = ValueGenerated.OnAdd;
                idProperty.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            }
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

            ApplyEntityTimestamps();
            List<PendingAuditEntry> pendingAuditEntries = CapturePendingAuditEntries();
            int result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            if (pendingAuditEntries.Count == 0)
            {
                return result;
            }

            await PersistAuditEntriesAsync(pendingAuditEntries, cancellationToken);
            return result;
        }

        private void ApplyEntityTimestamps()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            foreach (EntityEntry<Entity> entry in ChangeTracker.Entries<Entity>())
            {
                if (entry.Entity is AuditEntry or AuditPropertyChange)
                {
                    continue;
                }

                if (entry.State == EntityState.Added)
                {
                    entry.Entity.SetCreatedAt(now);
                    continue;
                }

                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.SetUpdatedAt(now);
                }
            }
        }

        private List<PendingAuditEntry> CapturePendingAuditEntries()
        {
            string? correlationId = ResolveCorrelationId();
            string? changedBy = currentUser?.UserId?.ToString();
            string? tenantId = tenantContext?.TenantId;
            DateTimeOffset changedAt = DateTimeOffset.UtcNow;

            return ChangeTracker.Entries<Entity>()
                .Where(entry =>
                    entry.Entity is not AuditEntry &&
                    entry.Entity is not AuditPropertyChange &&
                    entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(entry => PendingAuditEntry.Create(entry, changedAt, changedBy, correlationId, tenantId))
                .Where(entry => entry.PropertyChanges.Count > 0 || entry.Action != AuditAction.Update)
                .ToList();
        }

        private async Task PersistAuditEntriesAsync(IEnumerable<PendingAuditEntry> pendingAuditEntries, CancellationToken cancellationToken)
        {
            List<AuditEntry> auditEntries = pendingAuditEntries
                .Select(entry => entry.ToAuditEntry())
                .ToList();

            if (auditEntries.Count == 0)
            {
                return;
            }

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

        private static (Entity? parentEntity, string? parentEntityName) ResolveParent(EntityEntry<Entity> entry)
        {
            ReferenceEntry? parentReference = entry.References
                .FirstOrDefault(reference => reference.TargetEntry?.Entity is Entity);

            if (parentReference?.TargetEntry?.Entity is not Entity parentEntity)
            {
                return (null, null);
            }

            return (parentEntity, parentEntity.GetType().Name);
        }

        private static string? SerializeValue(object? value)
        {
            return value switch
            {
                null => null,
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O"),
                DateTime dateTime => dateTime.ToString("O"),
                byte[] bytes => Convert.ToBase64String(bytes),
                _ => value.ToString()
            };
        }

        private static AuditAction ResolveAction(EntityState state)
        {
            return state switch
            {
                EntityState.Added => AuditAction.Insert,
                EntityState.Modified => AuditAction.Update,
                EntityState.Deleted => AuditAction.Delete,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported audit state.")
            };
        }

        private string? ResolveCorrelationId()
        {
            if (Activity.Current is not null)
            {
                return Activity.Current.TraceId.ToString();
            }

            return null;
        }

        private sealed class PendingAuditEntry
        {
            public Entity Entity { get; init; } = null!;

            public string EntityName { get; init; } = string.Empty;

            public string? TenantId { get; init; }

            public AuditAction Action { get; init; }

            public DateTimeOffset ChangedAt { get; init; }

            public string? ChangedBy { get; init; }

            public string? CorrelationId { get; init; }

            public string? Source { get; init; }

            public Entity? ParentEntity { get; init; }

            public string? ParentEntityName { get; init; }

            public List<(string propertyName, string? oldValue, string? newValue)> PropertyChanges { get; init; } = [];

            public static PendingAuditEntry Create(EntityEntry<Entity> entry, DateTimeOffset changedAt, string? changedBy, string? correlationId, string? tenantId)
            {
                (Entity? parentEntity, string? parentEntityName) = ResolveParent(entry);
                List<(string propertyName, string? oldValue, string? newValue)> propertyChanges = CapturePropertyChanges(entry);

                return new PendingAuditEntry
                {
                    Entity = entry.Entity,
                    EntityName = entry.Entity.GetType().Name,
                    TenantId = tenantId,
                    Action = ResolveAction(entry.State),
                    ChangedAt = changedAt,
                    ChangedBy = changedBy,
                    CorrelationId = correlationId,
                    Source = "EntityFramework",
                    ParentEntity = parentEntity,
                    ParentEntityName = parentEntityName,
                    PropertyChanges = propertyChanges
                };
            }

            public AuditEntry ToAuditEntry()
            {
                string entityId = Entity.Id.ToString();
                string? parentEntityId = ParentEntity?.Id > 0 ? ParentEntity.Id.ToString() : null;
                AuditEntry auditEntry = new AuditEntry(EntityName, entityId, Action, ChangedAt, ChangedBy, CorrelationId, TenantId, Source, ParentEntityName, parentEntityId);

                foreach ((string propertyName, string? oldValue, string? newValue) in PropertyChanges)
                {
                    auditEntry.AddPropertyChange(propertyName, oldValue, newValue);
                }

                return auditEntry;
            }

            private static List<(string propertyName, string? oldValue, string? newValue)> CapturePropertyChanges(EntityEntry<Entity> entry)
            {
                IEnumerable<PropertyEntry> properties = entry.Properties.Where(property =>
                    !property.Metadata.IsPrimaryKey() &&
                    !property.Metadata.IsShadowProperty() &&
                    property.Metadata.Name is not nameof(Entity.CreatedAt) and not nameof(Entity.UpdatedAt));

                List<(string propertyName, string? oldValue, string? newValue)> changes = [];

                foreach (PropertyEntry property in properties)
                {
                    if (entry.State == EntityState.Modified && !property.IsModified)
                    {
                        continue;
                    }

                    string? oldValue = entry.State == EntityState.Added ? null : SerializeValue(property.OriginalValue);
                    string? newValue = entry.State == EntityState.Deleted ? null : SerializeValue(property.CurrentValue);

                    if (entry.State == EntityState.Modified && oldValue == newValue)
                    {
                        continue;
                    }

                    changes.Add((property.Metadata.Name, oldValue, newValue));
                }

                return changes;
            }
        }
    }
}
