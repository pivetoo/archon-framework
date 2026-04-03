using Archon.Application.MultiTenancy;
using Archon.Application.Persistence;
using Archon.Application.Services;
using Archon.Core.ValueObjects;
using Archon.Infrastructure.IdentityManagement;
using Archon.Infrastructure.Migrations;
using Archon.Infrastructure.MultiTenancy;
using Archon.Infrastructure.Persistence.Dapper;
using Archon.Infrastructure.Persistence.EF;
using Archon.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Archon.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddArchonMultiTenancy(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<TenantDatabaseOptions>(configuration);
            services.AddScoped<MultiTenantContext>();
            services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<MultiTenantContext>());
            services.AddSingleton<ITenantResolver, ConfigurationTenantResolver>();

            return services;
        }

        public static IServiceCollection AddArchonPersistence(this IServiceCollection services, IConfiguration configuration, params Assembly[] modelAssemblies)
        {
            services.AddArchonMultiTenancy(configuration);

            TenantDatabaseOptions tenantDatabaseOptions = BindTenantDatabaseOptions(configuration);
            ValidateTenantDatabases(tenantDatabaseOptions);

            services.AddSingleton(tenantDatabaseOptions);
            services.AddSingleton(new ModelAssemblyRegistry(GetModelAssemblies(modelAssemblies)));

            services.AddScoped<ArchonDbContext>(provider =>
            {
                ITenantContext tenantContext = provider.GetRequiredService<ITenantContext>();
                ModelAssemblyRegistry modelAssemblyRegistry = provider.GetRequiredService<ModelAssemblyRegistry>();

                (string connectionString, DatabaseProvider databaseProvider, string? schema) = ResolveCurrentTenant(tenantContext, tenantDatabaseOptions);
                DbContextOptions<ArchonDbContext> options = DbContextOptionsFactory.Create(connectionString, databaseProvider);

                return new ArchonDbContext(options, modelAssemblyRegistry, schema);
            });

            services.AddScoped<DbContext>(provider => provider.GetRequiredService<ArchonDbContext>());
            services.AddScoped<ISqlConnectionFactory, TenantSqlConnectionFactory>();
            services.AddScoped(typeof(ICrudService<>), typeof(CrudService<>));
            services.AddScoped(typeof(CrudService<>));

            return services;
        }

        public static IServiceCollection AddArchonIdentityManagement(this IServiceCollection services, IConfiguration configuration)
        {
            IdentityManagementOptions identityManagementOptions = new IdentityManagementOptions();
            configuration.GetSection("IdentityManagement").Bind(identityManagementOptions);

            JwtOptions jwtOptions = new JwtOptions();
            configuration.GetSection("Jwt").Bind(jwtOptions);

            services.AddSingleton(Options.Create(identityManagementOptions));
            services.AddSingleton(Options.Create(jwtOptions));
            services.AddHttpClient<IdentityManagementClient>();

            return services;
        }

        public static IServiceCollection RunMigrations(this IServiceCollection services, IConfiguration configuration, string schema, params Assembly[] migrationAssemblies)
        {
            if (!configuration.GetValue<bool>("RunMigrations", false))
            {
                return services;
            }

            List<(string name, string connectionString, DatabaseProvider databaseProvider)> connections = GetMigrationConnections(configuration);

            foreach ((string name, string connectionString, DatabaseProvider databaseProvider) in connections)
            {
                Console.WriteLine($"Running migrations for tenant: {name}");

                try
                {
                    DatabaseMigrator.Run(connectionString, schema, databaseProvider, migrationAssemblies);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"Migration failed for tenant {name}: {exception.Message}");
                }
            }

            return services;
        }

        private static TenantDatabaseOptions BindTenantDatabaseOptions(IConfiguration configuration)
        {
            TenantDatabaseOptions tenantDatabaseOptions = new TenantDatabaseOptions();
            configuration.Bind(tenantDatabaseOptions);

            return tenantDatabaseOptions;
        }

        private static void ValidateTenantDatabases(TenantDatabaseOptions tenantDatabaseOptions)
        {
            bool hasAnyConnection = tenantDatabaseOptions.TenantDatabases
                .Any(item => !string.IsNullOrWhiteSpace(item.Value.ConnectionString));

            if (!hasAnyConnection)
            {
                throw new InvalidOperationException("TenantDatabases must contain at least one valid connection string.");
            }
        }

        private static IReadOnlyCollection<Assembly> GetModelAssemblies(IEnumerable<Assembly> modelAssemblies)
        {
            List<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .ToList();

            foreach (Assembly assembly in modelAssemblies.Where(assembly => assembly is not null))
            {
                if (!assemblies.Contains(assembly))
                {
                    assemblies.Add(assembly);
                }
            }

            return assemblies;
        }

        private static List<(string name, string connectionString, DatabaseProvider databaseProvider)> GetMigrationConnections(IConfiguration configuration)
        {
            TenantDatabaseOptions tenantDatabaseOptions = BindTenantDatabaseOptions(configuration);

            List<(string name, string connectionString, DatabaseProvider databaseProvider)> connections = tenantDatabaseOptions.TenantDatabases
                .Select(item => (item.Key, item.Value.ConnectionString, item.Value.GetDatabaseProvider()))
                .Where(item => !string.IsNullOrWhiteSpace(item.ConnectionString))
                .ToList();

            if (connections.Count == 0)
            {
                throw new InvalidOperationException("No valid connection string was found in TenantDatabases.");
            }

            return connections;
        }

        private static (string connectionString, DatabaseProvider databaseProvider, string? schema) ResolveCurrentTenant(ITenantContext tenantContext, TenantDatabaseOptions tenantDatabaseOptions)
        {
            if (!string.IsNullOrWhiteSpace(tenantContext.ConnectionString))
            {
                return (tenantContext.ConnectionString, tenantContext.DatabaseProvider, tenantContext.Schema);
            }

            KeyValuePair<string, TenantDatabaseOption> fallbackTenant = tenantDatabaseOptions.TenantDatabases
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Value.ConnectionString));

            if (!string.IsNullOrWhiteSpace(fallbackTenant.Value?.ConnectionString))
            {
                return (
                    fallbackTenant.Value.ConnectionString,
                    fallbackTenant.Value.GetDatabaseProvider(),
                    fallbackTenant.Value.Schema);
            }

            throw new InvalidOperationException("No tenant connection string was configured for the current request.");
        }
    }
}
