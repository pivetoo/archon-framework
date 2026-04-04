using Archon.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Archon.Infrastructure.Persistence.EF
{
    internal static class ArchonModelConventions
    {
        private static readonly ValueConverter<DateTimeOffset, DateTimeOffset> UtcDateTimeOffsetConverter =
            new(
                value => value.ToUniversalTime(),
                value => value.ToUniversalTime());

        private static readonly ValueConverter<DateTimeOffset?, DateTimeOffset?> NullableUtcDateTimeOffsetConverter =
            new(
                value => value.HasValue ? value.Value.ToUniversalTime() : value,
                value => value.HasValue ? value.Value.ToUniversalTime() : value);

        public static void Apply(ModelBuilder modelBuilder)
        {
            ApplyEntityConventions(modelBuilder);
            ApplyPropertyConventions(modelBuilder);
            ApplyRelationshipConventions(modelBuilder);
        }

        public static void ApplyIdentifierConventions(ModelBuilder modelBuilder)
        {
            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                string? tableName = entityType.GetTableName();
                if (!string.IsNullOrWhiteSpace(tableName))
                {
                    entityType.SetTableName(tableName.ToLowerInvariant());
                }

                foreach (IMutableProperty property in entityType.GetProperties())
                {
                    property.SetColumnName(property.Name.ToLowerInvariant());
                }
            }
        }

        private static void ApplyEntityConventions(ModelBuilder modelBuilder)
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

                IMutableProperty? createdAtProperty = entityType.FindProperty(nameof(Entity.CreatedAt));
                if (createdAtProperty is not null)
                {
                    createdAtProperty.IsNullable = false;
                }

                IMutableProperty? updatedAtProperty = entityType.FindProperty(nameof(Entity.UpdatedAt));
                if (updatedAtProperty is not null)
                {
                    updatedAtProperty.IsNullable = true;
                }
            }
        }

        private static void ApplyPropertyConventions(ModelBuilder modelBuilder)
        {
            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (IMutableProperty property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(string) && property.GetMaxLength() is null)
                    {
                        property.SetMaxLength(255);
                    }

                    if (property.ClrType == typeof(decimal) && property.GetPrecision() is null)
                    {
                        property.SetPrecision(18);
                        property.SetScale(6);
                    }

                    if (property.ClrType == typeof(DateTimeOffset))
                    {
                        property.SetValueConverter(UtcDateTimeOffsetConverter);
                    }

                    if (property.ClrType == typeof(DateTimeOffset?))
                    {
                        property.SetValueConverter(NullableUtcDateTimeOffsetConverter);
                    }
                }
            }
        }

        private static void ApplyRelationshipConventions(ModelBuilder modelBuilder)
        {
            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (IMutableForeignKey foreignKey in entityType.GetForeignKeys())
                {
                    if (foreignKey.IsOwnership)
                    {
                        continue;
                    }

                    foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
                }
            }
        }
    }
}
