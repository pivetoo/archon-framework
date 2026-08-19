using Archon.Api.Attributes;
using Archon.Api.Contracts.Localization;
using Archon.Api.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace Archon.Api.Controllers
{
    // O catalogo e texto de interface, e a tela de login precisa dele ANTES de existir sessao.
    // Sem [AllowAnonymous] explicito, um app que feche o default do pipeline (FallbackPolicy)
    // devolve 401 aqui e a propria tela de login entra em loop de retentativa.
    [AllowAnonymous]
    public sealed class LocalizationController : ApiControllerBase
    {
        private readonly IStringLocalizerFactory stringLocalizerFactory;
        private readonly LocalizationCatalogOptions localizationCatalogOptions;

        public LocalizationController(IStringLocalizerFactory stringLocalizerFactory, LocalizationCatalogOptions localizationCatalogOptions)
        {
            this.stringLocalizerFactory = stringLocalizerFactory;
            this.localizationCatalogOptions = localizationCatalogOptions;
        }

        [GetEndpoint("catalog")]
        public IActionResult GetCatalog()
        {
            Dictionary<string, string> messages = new(StringComparer.Ordinal);

            MergeStrings(messages, Localizer);

            foreach (Type resourceType in localizationCatalogOptions.ResourceTypes)
            {
                IStringLocalizer localizer = stringLocalizerFactory.Create(resourceType);
                MergeStrings(messages, localizer);
            }

            LocalizationCatalogContract contract = new()
            {
                Culture = CultureInfo.CurrentCulture.Name,
                UICulture = CultureInfo.CurrentUICulture.Name,
                Messages = messages
            };

            return Http200(contract);
        }

        private static void MergeStrings(IDictionary<string, string> messages, IStringLocalizer localizer)
        {
            foreach (LocalizedString item in localizer.GetAllStrings(includeParentCultures: false))
            {
                messages[item.Name] = item.Value;
            }
        }
    }
}
