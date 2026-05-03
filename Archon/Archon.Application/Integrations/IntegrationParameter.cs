namespace Archon.Application.Integrations
{
    public sealed class IntegrationParameter
    {
        public string Key { get; init; } = string.Empty;

        public string Value { get; init; } = string.Empty;

        public bool IsSecret { get; init; }
    }
}
