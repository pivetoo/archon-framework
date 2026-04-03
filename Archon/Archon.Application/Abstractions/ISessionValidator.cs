namespace Archon.Application.Abstractions
{
    public interface ISessionValidator
    {
        Task<bool> IsSessionValidAsync(string sessionId, CancellationToken cancellationToken = default);
    }
}
