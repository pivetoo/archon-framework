namespace Archon.Api.Localization
{
    public sealed class ArchonLocalizationOptions
    {
        public string DefaultCulture { get; set; } = "pt-BR";

        public string[] SupportedCultures { get; set; } = ["pt-BR", "en-US", "es-AR"];
    }
}
