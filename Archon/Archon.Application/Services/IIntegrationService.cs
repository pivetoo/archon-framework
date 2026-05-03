using Archon.Application.Integrations;

namespace Archon.Application.Services
{
    public interface IIntegrationService
    {
        Task<Integration?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    }
}
