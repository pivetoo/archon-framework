using Archon.Core.ValueObjects;
using Archon.Infrastructure.MultiTenancy;

namespace Archon.Testing.Unit.Infrastructure.MultiTenancy
{
    public sealed class TenantDatabaseOptionTests
    {
        [TestCase("postgresql", DatabaseProvider.PostgreSql)]
        [TestCase("postgres", DatabaseProvider.PostgreSql)]
        [TestCase("sqlserver", DatabaseProvider.SqlServer)]
        [TestCase("mssql", DatabaseProvider.SqlServer)]
        [TestCase("mysql", DatabaseProvider.MySql)]
        [TestCase("unknown", DatabaseProvider.PostgreSql)]
        public void GetDatabaseProvider_ShouldResolveConfiguredDatabaseType(string databaseType, DatabaseProvider expectedProvider)
        {
            TenantDatabaseOption option = new TenantDatabaseOption
            {
                DatabaseType = databaseType
            };

            DatabaseProvider provider = option.GetDatabaseProvider();

            Assert.That(provider, Is.EqualTo(expectedProvider));
        }

        [Test]
        public void TenantDatabases_ShouldUseCaseInsensitiveKeys()
        {
            TenantDatabaseOptions options = new TenantDatabaseOptions
            {
                TenantDatabases = new Dictionary<string, TenantDatabaseOption>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Default"] = new TenantDatabaseOption
                    {
                        ConnectionString = "connection"
                    }
                }
            };

            Assert.That(options.TenantDatabases.ContainsKey("default"), Is.True);
            Assert.That(options.TenantDatabases.ContainsKey("DEFAULT"), Is.True);
        }
    }
}
