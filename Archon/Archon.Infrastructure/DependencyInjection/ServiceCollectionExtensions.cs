using Archon.Application.MultiTenancy;
using Archon.Infrastructure.MultiTenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    }
}
