namespace Archon.Core.Access
{
    // Uma capacidade e uma permissao de produto ("financeiro.aprovar") que agrupa varios endpoints.
    // O catalogo e derivado dos atributos dos controllers e sincronizado com o IdentityManagement.
    public sealed class AccessCapabilityModel
    {
        public string Key { get; init; } = string.Empty;

        public string Module { get; init; } = string.Empty;

        public string ModuleLabel { get; init; } = string.Empty;

        public int ModuleOrder { get; init; }

        public string Label { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public int Order { get; init; }

        public bool IsBaseline { get; init; }
    }

    public sealed class AccessCapabilitySyncRequest
    {
        public string SystemAudience { get; init; } = string.Empty;

        public List<AccessCapabilityModel> Capabilities { get; init; } = [];
    }
}
