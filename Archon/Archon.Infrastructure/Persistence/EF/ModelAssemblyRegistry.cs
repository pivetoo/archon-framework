using System.Reflection;

namespace Archon.Infrastructure.Persistence.EF
{
    public sealed class ModelAssemblyRegistry
    {
        public IReadOnlyCollection<Assembly> Assemblies { get; }

        public ModelAssemblyRegistry(IEnumerable<Assembly> assemblies)
        {
            Assemblies = assemblies
                .Where(assembly => assembly is not null)
                .Distinct()
                .ToList();
        }
    }
}
