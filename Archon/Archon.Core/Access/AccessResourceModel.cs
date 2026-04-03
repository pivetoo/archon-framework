namespace Archon.Core.Access
{
    public sealed class AccessResourceModel
    {
        public string Name { get; init; } = string.Empty;

        public string Controller { get; init; } = string.Empty;

        public string Action { get; init; } = string.Empty;

        public string HttpMethod { get; init; } = string.Empty;

        public string Route { get; init; } = string.Empty;
    }
}
