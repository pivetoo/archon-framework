using Archon.Core.ValueObjects;
using FluentMigrator.Runner;
using FluentMigrator.Runner.VersionTableInfo;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.Reflection;

namespace Archon.Infrastructure.Migrations
{
    public static class DatabaseMigrator
    {
        public static void Run(string connectionString, string schema, DatabaseProvider databaseProvider, params Assembly[] migrationAssemblies)
        {
            try
            {
                IServiceProvider serviceProvider = CreateServiceProvider(connectionString, schema, databaseProvider, migrationAssemblies);

                using IServiceScope scope = serviceProvider.CreateScope();
                IMigrationRunner runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Starting database migrations...");
                Console.ResetColor();

                runner.MigrateUp();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Database migrated successfully.");
                Console.ResetColor();
            }
            catch (Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Database migration failed.");
                Console.WriteLine(exception.Message);
                Console.ResetColor();
                throw;
            }
        }

        public static void Rollback(string connectionString, string schema, DatabaseProvider databaseProvider, long targetVersion, params Assembly[] migrationAssemblies)
        {
            try
            {
                IServiceProvider serviceProvider = CreateServiceProvider(connectionString, schema, databaseProvider, migrationAssemblies);

                using IServiceScope scope = serviceProvider.CreateScope();
                IMigrationRunner runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Rolling back database to version {targetVersion}...");
                Console.ResetColor();

                runner.MigrateDown(targetVersion);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Rollback completed successfully.");
                Console.ResetColor();
            }
            catch (Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Database rollback failed.");
                Console.WriteLine(exception.Message);
                Console.ResetColor();
                throw;
            }
        }

        private static IServiceProvider CreateServiceProvider(string connectionString, string schema, DatabaseProvider databaseProvider, Assembly[] migrationAssemblies)
        {
            ServiceCollection services = new ServiceCollection();
            string migrationConnectionString = BuildMigrationConnectionString(connectionString, schema, databaseProvider);

            services.AddFluentMigratorCore()
                .ConfigureRunner(builder =>
                {
                    ConfigureDatabase(builder, migrationConnectionString, databaseProvider);

                    foreach (Assembly assembly in migrationAssemblies)
                    {
                        builder.ScanIn(assembly).For.Migrations();
                    }

                    if (!string.IsNullOrWhiteSpace(schema))
                    {
                        builder.WithGlobalConnectionString(migrationConnectionString)
                            .WithVersionTable(new VersionTableMetadata(schema));
                    }
                })
                .AddLogging(logging => logging.AddFluentMigratorConsole());

            return services.BuildServiceProvider(false);
        }

        private static void ConfigureDatabase(IMigrationRunnerBuilder builder, string connectionString, DatabaseProvider databaseProvider)
        {
            switch (databaseProvider)
            {
                case DatabaseProvider.PostgreSql:
                    builder.AddPostgres().WithGlobalConnectionString(connectionString);
                    break;
                case DatabaseProvider.SqlServer:
                    builder.AddSqlServer().WithGlobalConnectionString(connectionString);
                    break;
                case DatabaseProvider.MySql:
                    builder.AddMySql5().WithGlobalConnectionString(connectionString);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(databaseProvider), databaseProvider, "Unsupported database provider.");
            }
        }

        private static string BuildMigrationConnectionString(string connectionString, string schema, DatabaseProvider databaseProvider)
        {
            if (databaseProvider != DatabaseProvider.PostgreSql || string.IsNullOrWhiteSpace(schema))
            {
                return connectionString;
            }

            DbConnectionStringBuilder builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (!builder.ContainsKey("Search Path"))
            {
                builder["Search Path"] = $"{schema},public";
            }

            return builder.ConnectionString;
        }

        private sealed class VersionTableMetadata : IVersionTableMetaData
        {
            public VersionTableMetadata(string schemaName)
            {
                SchemaName = schemaName;
            }

            public object? ApplicationContext { get; set; }

            public bool OwnsSchema => true;

            public string SchemaName { get; }

            public string TableName => "_migrations";

            public string ColumnName => "Version";

            public string DescriptionColumnName => "Description";

            public string UniqueIndexName => "IDX_Version";

            public string AppliedOnColumnName => "AppliedOn";

            public bool CreateWithPrimaryKey => false;
        }
    }
}
