namespace Archon.Api.Localization
{
    public sealed class LocalizationCatalogOptions
    {
        public IReadOnlyCollection<Type> ResourceTypes { get; init; } = Array.Empty<Type>();
    }
}
