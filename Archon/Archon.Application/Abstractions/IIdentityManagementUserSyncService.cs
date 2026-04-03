namespace Archon.Application.Abstractions
{
    public interface IIdentityManagementUserSyncService
    {
        Task<object?> GetOrCreateAsync(long externalUserId, string name, string? email, CancellationToken cancellationToken = default);
    }
}
