namespace Archon.Api.Attributes
{
    /// <summary>
    /// Modulo de produto do controller ("comercial", "financeiro"). Cada action vira a capacidade
    /// "{modulo}.{verbo}" com o verbo inferido do metodo HTTP: GET = ver, DELETE = excluir, demais =
    /// editar. Use <see cref="AccessCapabilityAttribute"/> na action para trocar a capacidade
    /// inferida (ex.: aprovar, movimentar) ou para compartilhar a leitura com outros modulos.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AccessModuleAttribute : Attribute
    {
        public string Module { get; }

        /// <summary>
        /// Modulos que tambem enxergam as leituras (GET) deste controller. Serve para cadastros
        /// auxiliares consultados por varias telas (bancos, plataformas, etapas do funil) sem abrir a
        /// escrita para eles.
        /// </summary>
        public string[] SharedRead { get; set; } = [];

        public AccessModuleAttribute(string module)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(module);
            Module = module.Trim();
        }
    }
}
