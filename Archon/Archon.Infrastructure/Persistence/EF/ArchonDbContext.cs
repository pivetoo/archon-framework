using Archon.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;

namespace Archon.Infrastructure.Persistence.EF
{
    public class ArchonDbContext : DbContext
    {
        private readonly IReadOnlyCollection<Assembly> modelAssemblies;
        private readonly string? schema;

        public ArchonDbContext(DbContextOptions<ArchonDbContext> options, ModelAssemblyRegistry modelAssemblyRegistry, string? schema = null) : base(options)
        {
            modelAssemblies = modelAssemblyRegistry.Assemblies;
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
    }
}
