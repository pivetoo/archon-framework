using Archon.Application.MultiTenancy;
using Archon.Core.ValueObjects;
using Archon.Infrastructure.Integrations;
using Archon.Infrastructure.MultiTenancy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Archon.Testing.Unit.Infrastructure.Integrations
{
    /// <summary>
    /// A tabela `integrations` guarda credenciais. Se o servico adivinhar o tenant quando nenhum foi
    /// resolvido, ele le credencial de outro cliente. Antes da correcao ele pegava o PRIMEIRO tenant
    /// configurado — o oposto do que o resto do framework faz.
    /// </summary>
    public sealed class IntegrationServiceTenantTests
    {
        private const string UnreachableConnectionString = "Host=127.0.0.1;Port=1;Database=archon_test;Username=x;Password=x;Timeout=1";

        private static IntegrationService CreateService(TenantDatabaseOptions tenantDatabaseOptions)
        {
            return new IntegrationService(
                new EmptyTenantContext(),
                new MemoryCache(new MemoryCacheOptions()),
                Options.Create(new IntegrationOptions()),
                tenantDatabaseOptions);
        }

        private static TenantDatabaseOptions BuildOptions(params string[] tenantNames)
        {
            TenantDatabaseOptions options = new();

            foreach (string tenantName in tenantNames)
            {
                options.TenantDatabases[tenantName] = new TenantDatabaseOption
                {
                    ConnectionString = UnreachableConnectionString,
                    DatabaseType = nameof(DatabaseProvider.PostgreSql)
                };
            }

            return options;
        }

        [Test]
        public void GetByNameAsync_ShouldRefuseToGuess_WhenMultipleTenantsConfiguredAndNoneResolved()
        {
            IntegrationService service = CreateService(BuildOptions("tenant-a", "tenant-b"));

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await service.GetByNameAsync("identity-management"));

            Assert.That(exception!.Message, Does.Contain("Refusing to fall back"));
        }

        [Test]
        public void GetByNameAsync_ShouldUseTheSingleTenant_WhenOnlyOneIsConfigured()
        {
            IntegrationService service = CreateService(BuildOptions("tenant-unico"));

            // Com um unico tenant nao ha outro para contaminar, entao assumir e seguro. A chamada passa
            // da resolucao e morre na conexao (porta 1 recusa na hora) — o que prova que nao parou antes.
            Assert.ThrowsAsync<Npgsql.NpgsqlException>(
                async () => await service.GetByNameAsync("identity-management"));
        }

        private sealed class EmptyTenantContext : ITenantContext
        {
            public string? TenantId => null;

            public string? CompanyName => null;

            public string? ApplicationId => null;

            public string? ConnectionString => null;

            public string? Schema => null;

            public DatabaseProvider DatabaseProvider => DatabaseProvider.PostgreSql;

            public bool HasTenant => false;
        }
    }
}
