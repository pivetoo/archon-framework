using Archon.Application.MultiTenancy;
using Archon.Core.ValueObjects;
using Archon.Infrastructure.MultiTenancy;

namespace Archon.Testing.Unit.Infrastructure.MultiTenancy
{
    public sealed class MultiTenantContextTests
    {
        [Test]
        public void SetTenant_ShouldPopulateContext()
        {
            MultiTenantContext context = new MultiTenantContext();
            TenantInfo tenant = new TenantInfo
            {
                TenantId = "tenant-a",
                CompanyName = "Archon",
                ApplicationId = "archon-app",
                ConnectionString = "Host=localhost;Database=archon;",
                Schema = "public",
                DatabaseProvider = DatabaseProvider.PostgreSql
            };

            context.SetTenant(tenant);

            Assert.That(context.HasTenant, Is.True);
            Assert.That(context.TenantId, Is.EqualTo("tenant-a"));
            Assert.That(context.CompanyName, Is.EqualTo("Archon"));
            Assert.That(context.ApplicationId, Is.EqualTo("archon-app"));
            Assert.That(context.ConnectionString, Is.EqualTo("Host=localhost;Database=archon;"));
            Assert.That(context.Schema, Is.EqualTo("public"));
            Assert.That(context.DatabaseProvider, Is.EqualTo(DatabaseProvider.PostgreSql));
        }

        [Test]
        public void Clear_ShouldResetContext()
        {
            MultiTenantContext context = new MultiTenantContext();
            context.SetTenant(new TenantInfo
            {
                TenantId = "tenant-a",
                ConnectionString = "connection-string",
                DatabaseProvider = DatabaseProvider.SqlServer
            });

            context.Clear();

            Assert.That(context.HasTenant, Is.False);
            Assert.That(context.TenantId, Is.Null);
            Assert.That(context.CompanyName, Is.Null);
            Assert.That(context.ApplicationId, Is.Null);
            Assert.That(context.ConnectionString, Is.Null);
            Assert.That(context.Schema, Is.Null);
            Assert.That(context.DatabaseProvider, Is.EqualTo(DatabaseProvider.PostgreSql));
        }

        [Test]
        public void SetTenant_ShouldThrow_WhenTenantInfoIsNull()
        {
            MultiTenantContext context = new MultiTenantContext();

            Assert.Throws<ArgumentNullException>(() => context.SetTenant(null!));
        }
    }
}
