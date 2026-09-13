using Microsoft.Extensions.Localization;

namespace Archon.Api.Localization
{
    /// <summary>
    /// Resolve uma chave i18n procurando primeiro nos resources da aplicacao e depois no do framework.
    /// Compartilhado pelo middleware de excecoes e pelas respostas de lote, que precisam traduzir o
    /// motivo de cada registro que falhou com a mesma regra.
    /// </summary>
    internal static class LocalizedMessageResolver
    {
        public static bool TryResolve(
            IStringLocalizer<ArchonApiResource> archonLocalizer,
            IStringLocalizerFactory factory,
            LocalizationCatalogOptions catalog,
            string? message,
            object[]? args,
            out string resolved)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                resolved = archonLocalizer["error.unexpected.short"];
                return false;
            }

            object[] formatArgs = args ?? [];

            foreach (Type resourceType in catalog.ResourceTypes)
            {
                IStringLocalizer appLocalizer = factory.Create(resourceType);
                LocalizedString localized = formatArgs.Length > 0
                    ? appLocalizer[message, formatArgs]
                    : appLocalizer[message];
                if (!localized.ResourceNotFound)
                {
                    resolved = localized.Value;
                    return true;
                }
            }

            LocalizedString archon = formatArgs.Length > 0
                ? archonLocalizer[message, formatArgs]
                : archonLocalizer[message];
            if (!archon.ResourceNotFound)
            {
                resolved = archon.Value;
                return true;
            }

            resolved = message;
            return false;
        }
    }
}
