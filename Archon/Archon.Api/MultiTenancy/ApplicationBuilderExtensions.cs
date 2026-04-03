namespace Archon.Api.MultiTenancy
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseArchonApi(this IApplicationBuilder app)
        {
            return app.UseMiddleware<TenantResolutionMiddleware>();
        }
    }
}
