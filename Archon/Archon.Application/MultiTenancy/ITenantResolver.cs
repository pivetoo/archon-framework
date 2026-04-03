namespace Archon.Application.MultiTenancy
{
    public interface ITenantResolver
    {
        Task<TenantInfo?> ResolveAsync(string? applicationId, CancellationToken cancellationToken = default);
    }
}
