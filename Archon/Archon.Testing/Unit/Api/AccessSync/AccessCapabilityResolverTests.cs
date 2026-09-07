using System.Reflection;
using Archon.Api.AccessSync;
using Archon.Api.Attributes;
using Archon.Core.Access;

namespace Archon.Testing.Unit.Api.AccessSync
{
    public sealed class AccessCapabilityResolverTests
    {
        [AccessModule("financeiro")]
        private sealed class FinanceiroController
        {
            public void Get() { }

            public void Create() { }

            public void Delete() { }

            [AccessCapability("financeiro.aprovar")]
            public void Approve() { }

            [AccessCapability("financeiro.ver", "producao.ver")]
            public void GetByCampaign() { }
        }

        [AccessModule("relatorios")]
        [AccessCapability("relatorios.financeiro")]
        private sealed class RelatoriosController
        {
            public void GetCashFlow() { }
        }

        private sealed class SemModuloController
        {
            public void Get() { }
        }

        [AccessModule("configuracoes", SharedRead = ["financeiro", "producao"])]
        private sealed class BancosController
        {
            public void Get() { }

            public void Create() { }
        }

        private static MethodInfo Method<T>(string name)
        {
            return typeof(T).GetMethod(name)!;
        }

        [Test]
        public void Resolve_ShouldInferVerbFromHttpMethod_WhenOnlyModuleIsDeclared()
        {
            Assert.That(AccessCapabilityResolver.Resolve(typeof(FinanceiroController), Method<FinanceiroController>("Get"), "GET"), Is.EqualTo(new[] { "financeiro.ver" }));
            Assert.That(AccessCapabilityResolver.Resolve(typeof(FinanceiroController), Method<FinanceiroController>("Create"), "POST"), Is.EqualTo(new[] { "financeiro.editar" }));
            Assert.That(AccessCapabilityResolver.Resolve(typeof(FinanceiroController), Method<FinanceiroController>("Delete"), "DELETE"), Is.EqualTo(new[] { "financeiro.excluir" }));
        }

        [Test]
        public void Resolve_ShouldPreferExplicitCapabilities_OverInference()
        {
            Assert.That(AccessCapabilityResolver.Resolve(typeof(FinanceiroController), Method<FinanceiroController>("Approve"), "POST"), Is.EqualTo(new[] { "financeiro.aprovar" }));
            Assert.That(AccessCapabilityResolver.Resolve(typeof(FinanceiroController), Method<FinanceiroController>("GetByCampaign"), "GET"), Is.EqualTo(new[] { "financeiro.ver", "producao.ver" }));
            Assert.That(AccessCapabilityResolver.Resolve(typeof(RelatoriosController), Method<RelatoriosController>("GetCashFlow"), "GET"), Is.EqualTo(new[] { "relatorios.financeiro" }));
        }

        [Test]
        public void Resolve_ShouldShareReadsWithOtherModules_ButKeepWritesInTheOwnerModule()
        {
            Assert.That(AccessCapabilityResolver.Resolve(typeof(BancosController), Method<BancosController>("Get"), "GET"), Is.EqualTo(new[] { "configuracoes.ver", "financeiro.ver", "producao.ver" }));
            Assert.That(AccessCapabilityResolver.Resolve(typeof(BancosController), Method<BancosController>("Create"), "POST"), Is.EqualTo(new[] { "configuracoes.editar" }));
        }

        [Test]
        public void Resolve_ShouldReturnEmpty_WhenControllerHasNoModule()
        {
            Assert.That(AccessCapabilityResolver.Resolve(typeof(SemModuloController), Method<SemModuloController>("Get"), "GET"), Is.Empty);
        }

        [Test]
        public void BuildCatalog_ShouldOrderModulesAndVerbs_AndResolveLabels()
        {
            List<AccessResourceModel> resources =
            [
                new AccessResourceModel { Name = "a", Capabilities = ["financeiro.editar", "producao.ver"] },
                new AccessResourceModel { Name = "b", Capabilities = ["financeiro.ver"] },
                new AccessResourceModel { Name = "c", Capabilities = ["comercial.ver", "geral.basico"] },
                new AccessResourceModel { Name = "d", Capabilities = ["financeiro.aprovar"] },
                new AccessResourceModel { Name = "e", Capabilities = [] }
            ];

            AccessCatalogAttribute catalog = new AccessCatalogAttribute
            {
                Modules = ["comercial", "producao", "financeiro"],
                Verbs = ["ver", "editar", "aprovar"],
                Baseline = ["geral.basico"]
            };

            Dictionary<string, string> translations = new(StringComparer.Ordinal)
            {
                ["accessModule.financeiro"] = "Financeiro",
                ["accessCapability.financeiro.aprovar"] = "Aprovar pagamentos",
                ["accessCapability.financeiro.aprovar.description"] = "Libera pagamentos acima da alcada."
            };

            List<AccessCapabilityModel> result = AccessCapabilityResolver.BuildCatalog(resources, catalog, key => translations.GetValueOrDefault(key));

            Assert.That(result.Select(item => item.Key), Is.EqualTo(new[]
            {
                "comercial.ver", "producao.ver", "financeiro.ver", "financeiro.editar", "financeiro.aprovar", "geral.basico"
            }));

            AccessCapabilityModel aprovar = result.Single(item => item.Key == "financeiro.aprovar");
            Assert.That(aprovar.Module, Is.EqualTo("financeiro"));
            Assert.That(aprovar.ModuleLabel, Is.EqualTo("Financeiro"));
            Assert.That(aprovar.ModuleOrder, Is.EqualTo(3));
            Assert.That(aprovar.Order, Is.EqualTo(3));
            Assert.That(aprovar.Label, Is.EqualTo("Aprovar pagamentos"));
            Assert.That(aprovar.Description, Is.EqualTo("Libera pagamentos acima da alcada."));
            Assert.That(aprovar.IsBaseline, Is.False);

            AccessCapabilityModel basico = result.Single(item => item.Key == "geral.basico");
            Assert.That(basico.ModuleLabel, Is.EqualTo("Geral"));
            Assert.That(basico.Label, Is.EqualTo("Basico"));
            Assert.That(basico.Description, Is.Empty);
            Assert.That(basico.IsBaseline, Is.True);
            Assert.That(basico.ModuleOrder, Is.EqualTo(4));
        }

        [Test]
        public void BuildCatalog_ShouldFallBackToAlphabeticalOrder_WithoutCatalogAttribute()
        {
            List<AccessResourceModel> resources =
            [
                new AccessResourceModel { Name = "a", Capabilities = ["producao.editar", "producao.ver"] },
                new AccessResourceModel { Name = "b", Capabilities = ["comercial.ver"] }
            ];

            List<AccessCapabilityModel> result = AccessCapabilityResolver.BuildCatalog(resources, null, _ => null);

            Assert.That(result.Select(item => item.Key), Is.EqualTo(new[] { "comercial.ver", "producao.editar", "producao.ver" }));
        }
    }
}
