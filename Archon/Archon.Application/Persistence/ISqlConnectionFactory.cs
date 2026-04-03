using System.Data.Common;

namespace Archon.Application.Persistence
{
    public interface ISqlConnectionFactory
    {
        DbConnection CreateConnection();

        Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
    }
}
