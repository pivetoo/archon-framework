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
                ["TenantDatabases:default:CompanyName"] = "Archon Testing",
                ["TenantDatabases:default:ApplicationId"] = "archon-testing",
                ["TenantDatabases:default:ConnectionString"] = "Host=localhost;Database=archon_testing;Username=test;Password=test",
                ["TenantDatabases:default:DatabaseType"] = "PostgreSql",
                ["TenantDatabases:default:Schema"] = "public"
            });

            builder.Services.AddArchonApi(builder.Configuration);
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(TestApiController).Assembly);

            WebApplication app = builder.Build();
            app.UseArchonApi();
            app.MapControllers();

            await app.StartAsync();
            return app;
        }
    }
}
