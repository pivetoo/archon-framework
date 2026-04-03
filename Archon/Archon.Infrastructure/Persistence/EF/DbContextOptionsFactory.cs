using Archon.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Archon.Infrastructure.Persistence.EF
{
    public static class DbContextOptionsFactory
    {
        public static DbContextOptions<ArchonDbContext> Create(string connectionString, DatabaseProvider databaseProvider)
        {
            DbContextOptionsBuilder<ArchonDbContext> optionsBuilder = new DbContextOptionsBuilder<ArchonDbContext>();
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

            switch (databaseProvider)
            {
                case DatabaseProvider.PostgreSql:
                    optionsBuilder.UseNpgsql(connectionString);
                    break;
                case DatabaseProvider.SqlServer:
                    optionsBuilder.UseSqlServer(connectionString);
                    break;
                case DatabaseProvider.MySql:
                    optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(databaseProvider), databaseProvider, "Unsupported database provider.");
            }

            return optionsBuilder.Options;
        }
    }
}
