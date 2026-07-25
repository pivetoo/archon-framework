using Archon.Api.DependencyInjection;
using Archon.Api.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Archon.Testing.Integration.Support
{
    internal static class TestApiHost
    {
        public static async Task<WebApplication> CreateAsync()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing"
            });

            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantDatabases:FixedTenantId:CompanyName"] = "Archon Testing",
                ["TenantDatabases:FixedTenantId:ApplicationId"] = "archon-testing",
                ["TenantDatabases:FixedTenantId:ConnectionString"] = "Host=localhost;Database=archon_testing;Username=test;Password=test",
                ["TenantDatabases:FixedTenantId:DatabaseType"] = "PostgreSql",
                ["TenantDatabases:FixedTenantId:Schema"] = "public"
            });

            builder.Services.AddArchonApi(builder.Configuration);
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(TestApiController).Assembly);

            WebApplication app = builder.Build();
            app.UseArchonApi();
            // Espelha a ordem real do pipeline: a resolucao de tenant vem depois da autenticacao, para
            // ler apenas claim validada. O host de teste roda em modo FixedTenantId, que nao depende de
            // claim, mas o registro precisa existir na mesma posicao.
            app.UseArchonTenantResolution();
            app.MapControllers();

            await app.StartAsync();
            return app;
        }
    }
}
