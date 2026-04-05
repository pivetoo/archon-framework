using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Archon.Api.Localization
{
    public sealed class ConfigureRequestLocalizationOptions : IConfigureOptions<RequestLocalizationOptions>
    {
        private readonly ArchonLocalizationOptions options;

        public ConfigureRequestLocalizationOptions(IOptions<ArchonLocalizationOptions> options)
        {
            this.options = options.Value;
        }

        public void Configure(RequestLocalizationOptions requestLocalizationOptions)
        {
            string defaultCulture = string.IsNullOrWhiteSpace(options.DefaultCulture)
                ? "pt-BR"
                : options.DefaultCulture;

            string[] supportedCultureNames = options.SupportedCultures is { Length: > 0 }
                ? options.SupportedCultures
                : ["pt-BR", "en-US", "es-AR"];

            List<CultureInfo> supportedCultures = supportedCultureNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new CultureInfo(name))
                .ToList();

            requestLocalizationOptions.DefaultRequestCulture = new RequestCulture(defaultCulture);
            requestLocalizationOptions.SupportedCultures = supportedCultures;
            requestLocalizationOptions.SupportedUICultures = supportedCultures;

            requestLocalizationOptions.RequestCultureProviders = [
                new CustomRequestCultureProvider(context =>
                {
                    string? queryCulture = context.Request.Query["lang"].FirstOrDefault()
                        ?? context.Request.Query["culture"].FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(queryCulture))
                    {
                        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(queryCulture));
                    }

                    string? headerCulture = context.Request.Headers["X-Language"].FirstOrDefault()
                        ?? context.Request.Headers["Accept-Language"].FirstOrDefault()?.Split(',').FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(headerCulture))
                    {
                        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(headerCulture));
                    }

                    return Task.FromResult<ProviderCultureResult?>(null);
                }),
                new CookieRequestCultureProvider(),
                new AcceptLanguageHeaderRequestCultureProvider()
            ];
        }
    }
}
