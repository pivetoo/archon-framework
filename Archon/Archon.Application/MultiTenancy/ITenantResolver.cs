namespace Archon.Application.MultiTenancy
{
    public interface ITenantResolver
    {
        /// <summary>
        /// Resolve o tenant pelo identificador dele. O parametro ja se chamou `applicationId`, mas as
        /// duas implementacoes sempre trataram o valor como tenant — quem lesse a interface e confiasse
        /// no nome passava o argumento errado.
        /// </summary>
        Task<TenantInfo?> ResolveAsync(string? tenantId, CancellationToken cancellationToken = default);

        Task<TenantInfo?> ResolveByTenantAndApiKeyAsync(string? tenantId, string? apiKey, CancellationToken cancellationToken = default);

        Task<TenantInfo?> ResolveByApiKeyAsync(string? apiKey, CancellationToken cancellationToken = default);
    }
}
