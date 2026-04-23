using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

namespace Archon.Api.DependencyInjection
{
    public static class OpenTelemetryExtensions
    {
        public static IServiceCollection AddArchonOpenTelemetry(
            this IServiceCollection services,
            IConfiguration configuration,
            string serviceName,
            string? serviceVersion = null)
        {
            string? otlpEndpoint = configuration["OpenTelemetry:Endpoint"];

            if (string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                return services;
            }

            services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing
                        .SetResourceBuilder(ResourceBuilder.CreateDefault()
                            .AddService(serviceName, serviceVersion: serviceVersion))
                        .AddAspNetCoreInstrumentation(options =>
                        {
                            options.RecordException = true;
                            options.Filter = context =>
                            {
                                string path = context.Request.Path.Value ?? string.Empty;
                                return !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase);
                            };
                        })
                        .AddHttpClientInstrumentation()
                        .AddSource("Archon")
                        .AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(otlpEndpoint);
                        });
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .SetResourceBuilder(ResourceBuilder.CreateDefault()
                            .AddService(serviceName, serviceVersion: serviceVersion))
                        .AddAspNetCoreInstrumentation()
                        .AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(otlpEndpoint);
                        });
                });

            return services;
        }
    }
}
