using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Archon.Infrastructure.MultiTenancy
{
    public static class TenantCatalogBootstrap
    {
        private const string SnapshotFileName = ".archon-tenants-snapshot.json";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private static readonly object HydrationLock = new();
        private static readonly Dictionary<string, IReadOnlyList<BootstrapTenant>> HydrationCache = new();

        public static IReadOnlyList<BootstrapTenant> Hydrate(IConfiguration configuration)
        {
            IdentityCatalogOptions options = new();
            configuration.GetSection("IdentityCatalog").Bind(options);

            if (!options.IsConfigured)
            {
                return Array.Empty<BootstrapTenant>();
            }

            string cacheKey = $"{options.BaseUrl}|{options.ApplicationId}";

            lock (HydrationLock)
            {
                if (HydrationCache.TryGetValue(cacheKey, out IReadOnlyList<BootstrapTenant>? cached))
                {
                    return cached;
                }

                IReadOnlyList<BootstrapTenant> resolved = HydrateOnce(configuration, options);
                HydrationCache[cacheKey] = resolved;
                return resolved;
            }
        }

        private static IReadOnlyList<BootstrapTenant> HydrateOnce(IConfiguration configuration, IdentityCatalogOptions options)
        {
            string snapshotPath = ResolveSnapshotPath(configuration);

            try
            {
                IReadOnlyList<BootstrapTenant> fresh = FetchFromCatalog(options);
                if (fresh.Count > 0)
                {
                    PersistSnapshot(snapshotPath, fresh);
                    Console.WriteLine($"TenantCatalogBootstrap: fetched {fresh.Count} tenants from IdentityCatalog and persisted snapshot.");
                    return fresh;
                }

                Console.WriteLine("TenantCatalogBootstrap: IdentityCatalog returned empty list. Falling back to snapshot.");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"TenantCatalogBootstrap: failed to fetch from IdentityCatalog ({exception.GetType().Name}: {exception.Message}). Falling back to snapshot.");
            }

            IReadOnlyList<BootstrapTenant> snapshot = LoadSnapshot(snapshotPath);
            if (snapshot.Count > 0)
            {
                Console.WriteLine($"TenantCatalogBootstrap: loaded {snapshot.Count} tenants from snapshot at {snapshotPath}.");
            }
            else
            {
                Console.WriteLine("TenantCatalogBootstrap: no snapshot available. System will rely on TenantDatabases from appsettings (if any).");
            }

            return snapshot;
        }

        public static void OverlayOnConfiguration(IConfigurationBuilder builder, IReadOnlyList<BootstrapTenant> tenants)
        {
            if (tenants.Count == 0)
            {
                return;
            }

            Dictionary<string, string?> overlay = new();
            foreach (BootstrapTenant tenant in tenants)
            {
                string prefix = $"TenantDatabases:{tenant.TenantId}";
                overlay[$"{prefix}:CompanyName"] = tenant.CompanyName;
                overlay[$"{prefix}:ApplicationId"] = tenant.ApplicationId;
                overlay[$"{prefix}:ConnectionString"] = tenant.ConnectionString;
                overlay[$"{prefix}:Schema"] = tenant.Schema;
                overlay[$"{prefix}:DatabaseType"] = MapDatabaseType(tenant.DatabaseProvider);
                overlay[$"{prefix}:ApiKey"] = tenant.ApiKey;
            }

            builder.AddInMemoryCollection(overlay);
        }

        private static IReadOnlyList<BootstrapTenant> FetchFromCatalog(IdentityCatalogOptions options)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = options.RequestTimeout;
            client.DefaultRequestHeaders.Add("X-Api-Key", options.ResolvedApiKey);

            string url = $"api/Tenants/ListByApplication?applicationId={Uri.EscapeDataString(options.ApplicationId)}";

            HttpResponseMessage response = client.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            TenantListEnvelope? envelope = response.Content.ReadFromJsonAsync<TenantListEnvelope>(JsonOptions).GetAwaiter().GetResult();
            return envelope?.Data?.Where(item => !string.IsNullOrWhiteSpace(item.ConnectionString)).ToList() ?? new List<BootstrapTenant>();
        }

        private static void PersistSnapshot(string path, IReadOnlyList<BootstrapTenant> tenants)
        {
            try
            {
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(tenants, JsonOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"TenantCatalogBootstrap: failed to persist snapshot to {path}: {exception.Message}");
            }
        }

        private static IReadOnlyList<BootstrapTenant> LoadSnapshot(string path)
        {
            if (!File.Exists(path))
            {
                return Array.Empty<BootstrapTenant>();
            }

            try
            {
                string json = File.ReadAllText(path);
                List<BootstrapTenant>? tenants = JsonSerializer.Deserialize<List<BootstrapTenant>>(json, JsonOptions);
                return tenants?.Where(item => !string.IsNullOrWhiteSpace(item.ConnectionString)).ToList() ?? new List<BootstrapTenant>();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"TenantCatalogBootstrap: failed to load snapshot from {path}: {exception.Message}");
                return Array.Empty<BootstrapTenant>();
            }
        }

        private static string ResolveSnapshotPath(IConfiguration configuration)
        {
            string? configured = configuration["IdentityCatalog:SnapshotPath"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            return Path.Combine(AppContext.BaseDirectory, SnapshotFileName);
        }

        private static string MapDatabaseType(int provider)
        {
            return provider switch
            {
                1 => "PostgreSql",
                2 => "SqlServer",
                3 => "MySql",
                _ => "PostgreSql"
            };
        }

        private sealed class TenantListEnvelope
        {
            [JsonPropertyName("data")]
            public List<BootstrapTenant>? Data { get; set; }
        }
    }

    public sealed class BootstrapTenant
    {
        public string TenantId { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string ApplicationId { get; set; } = string.Empty;

        public string ConnectionString { get; set; } = string.Empty;

        public int DatabaseProvider { get; set; } = 1;

        public string Schema { get; set; } = "public";

        public string ApiKey { get; set; } = string.Empty;
    }
}
