namespace Archon.Api.Contracts.Localization
{
    public sealed class LocalizationCatalogContract
    {
        public required string Culture { get; init; }

        public required string UICulture { get; init; }

        public required IReadOnlyDictionary<string, string> Messages { get; init; }
    }
}
