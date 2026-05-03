namespace Archon.Application.Integrations
{
    public sealed class Integration
    {
        public string Name { get; init; } = string.Empty;

        public string BaseUrl { get; init; } = string.Empty;

        public IReadOnlyCollection<IntegrationParameter> Parameters { get; init; } = Array.Empty<IntegrationParameter>();

        public string? GetParameter(string key)
        {
            return Parameters.FirstOrDefault(parameter => string.Equals(parameter.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        public IReadOnlyDictionary<string, string> GetParametersByPrefix(string prefix)
        {
            return Parameters
                .Where(parameter => parameter.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    parameter => parameter.Key[prefix.Length..],
                    parameter => parameter.Value,
                    StringComparer.OrdinalIgnoreCase);
        }
    }
}
