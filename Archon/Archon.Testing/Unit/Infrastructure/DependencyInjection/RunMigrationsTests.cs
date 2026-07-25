using Archon.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Archon.Testing.Unit.Infrastructure.DependencyInjection
{
    public sealed class RunMigrationsTests
    {
        // Porta 1 recusa conexao na hora, entao a falha e imediata e o teste nao depende de timeout.
        private const string UnreachableConnectionString = "Host=127.0.0.1;Port=1;Database=archon_test;Username=x;Password=x;Timeout=1";

        private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
        {
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        [Test]
        public void RunMigrations_ShouldThrow_WhenMigrationFails()
        {
            IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["RunMigrations"] = "true",
                ["TenantDatabases:tenant-a:ConnectionString"] = UnreachableConnectionString,
                ["TenantDatabases:tenant-a:DatabaseType"] = "PostgreSql"
            });

            ServiceCollection services = new();

            // Subir com schema parcialmente migrado corrompe dado em silencio. Antes da correcao a
            // excecao era apenas impressa no console e a aplicacao subia normalmente.
            InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() =>
                services.RunMigrations(configuration, "public", Assembly.GetExecutingAssembly()));

            Assert.That(exception!.Message, Does.Contain("tenant-a"));
            Assert.That(exception.Message, Does.Contain("will not start"));
        }

        [Test]
        public void RunMigrations_ShouldReportEveryFailingTenant_NotOnlyTheFirst()
        {
            IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["RunMigrations"] = "true",
                ["TenantDatabases:tenant-a:ConnectionString"] = UnreachableConnectionString,
                ["TenantDatabases:tenant-a:DatabaseType"] = "PostgreSql",
                ["TenantDatabases:tenant-b:ConnectionString"] = UnreachableConnectionString,
                ["TenantDatabases:tenant-b:DatabaseType"] = "PostgreSql"
            });

            ServiceCollection services = new();

            InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() =>
                services.RunMigrations(configuration, "public", Assembly.GetExecutingAssembly()));

            // O laco nao para no primeiro erro: o operador precisa ver todos os tenants quebrados de
            // uma vez, em vez de descobrir um por deploy.
            Assert.That(exception!.Message, Does.Contain("tenant-a"));
            Assert.That(exception.Message, Does.Contain("tenant-b"));
            Assert.That(exception.Message, Does.Contain("2 tenant(s)"));
        }

        [Test]
        public void RunMigrations_ShouldRegisterRunnerAndNotThrow_WhenDisabled()
        {
            IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["RunMigrations"] = "false",
                ["TenantDatabases:tenant-a:ConnectionString"] = UnreachableConnectionString
            });

            ServiceCollection services = new();

            Assert.DoesNotThrow(() => services.RunMigrations(configuration, "public", Assembly.GetExecutingAssembly()));

            // O runner e dependencia do TenantBootstrapService e precisa existir mesmo com a flag off.
            Assert.That(
                services.Any(descriptor => descriptor.ServiceType == typeof(Archon.Infrastructure.Migrations.TenantMigrationRunner)),
                Is.True);
        }
    }
}
