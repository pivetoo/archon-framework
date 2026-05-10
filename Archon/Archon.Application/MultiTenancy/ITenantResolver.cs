namespace Archon.Application.MultiTenancy
{
    public interface ITenantResolver
    {
        Task<TenantInfo?> ResolveAsync(string? applicationId, CancellationToken cancellationToken = default);

        Task<TenantInfo?> ResolveByApiKeyAsync(string? apiKey, CancellationToken cancellationToken = default);

        [Obsolete("Use ResolveByApiKeyAsync. Mantido apenas para compatibilidade durante a transicao.")]
        Task<TenantInfo?> ResolveBySecretAsync(string? integrationSecret, CancellationToken cancellationToken = default)
            => ResolveByApiKeyAsync(integrationSecret, cancellationToken);
    }
}
