using System.Diagnostics;
using Archon.Application.Abstractions;
using Archon.Application.MultiTenancy;
using Archon.Core.Entities;
using Archon.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Archon.Infrastructure.Persistence.EF
{
    internal sealed class ArchonAuditManager
    {
        private readonly ChangeTracker changeTracker;
        private readonly ICurrentUser? currentUser;
        private readonly ITenantContext? tenantContext;

        public ArchonAuditManager(ChangeTracker changeTracker, ICurrentUser? currentUser = null, ITenantContext? tenantContext = null)
        {
            this.changeTracker = changeTracker;
            this.currentUser = currentUser;
            this.tenantContext = tenantContext;
        }

        public void ApplyEntityTimestamps()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            foreach (EntityEntry<Entity> entry in changeTracker.Entries<Entity>())
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

        public List<AuditEntry> CreateAuditEntries()
        {
            string? correlationId = ResolveCorrelationId();
            string? changedBy = currentUser?.UserId?.ToString();
            string? tenantId = tenantContext?.TenantId;
            DateTimeOffset changedAt = DateTimeOffset.UtcNow;

            return changeTracker.Entries<Entity>()
                .Where(entry =>
                    entry.Entity is not AuditEntry &&
                    entry.Entity is not AuditPropertyChange &&
                    entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(entry => PendingAuditEntry.Create(entry, changedAt, changedBy, correlationId, tenantId))
                .Where(entry => entry.PropertyChanges.Count > 0 || entry.Action != AuditAction.Update)
                .Select(entry => entry.ToAuditEntry())
                .ToList();
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

        private static string? ResolveCorrelationId()
        {
            return Activity.Current?.TraceId.ToString();
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
