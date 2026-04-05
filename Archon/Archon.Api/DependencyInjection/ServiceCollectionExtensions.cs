using Archon.Api.AccessSync;
using Archon.Api.Localization;
using Archon.Api.Security;
using Archon.Api.Security.Authentication;
using Archon.Application.Abstractions;
using Archon.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Archon.Api.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddArchonApi(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddLocalization(options =>
            {
                options.ResourcesPath = "Resources/Localization";
            });
            services.Configure<ArchonLocalizationOptions>(configuration.GetSection("Archon:Localization"));
            services.AddSingleton<IConfigureOptions<RequestLocalizationOptions>, ConfigureRequestLocalizationOptions>();
            services.AddArchonMultiTenancy(configuration);
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });
            services.AddOpenApi();

            return services;
        }

        public static AuthenticationBuilder AddArchonAuthentication(this IServiceCollection services, IConfiguration configuration, string scheme = "DynamicJwtBearer")
        {
            services.AddArchonIdentityManagement(configuration);
            services.AddScoped<DynamicJwtValidator>();

            return services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = scheme;
                    options.DefaultChallengeScheme = scheme;
                })
                .AddScheme<AuthenticationSchemeOptions, DynamicJwtBearerHandler>(scheme, _ => { });
        }
    }
}
