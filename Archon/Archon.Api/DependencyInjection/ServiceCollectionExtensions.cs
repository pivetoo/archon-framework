using Archon.Api.Security;
using Archon.Application.Abstractions;
using Archon.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Archon.Api.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddArchonApi(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddArchonMultiTenancy(configuration);
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
            services.AddOpenApi();

            return services;
        }
    }
}
