using System.Reflection;
using Archon.Api.Attributes;
using Archon.Core.Access;

namespace Archon.Api.AccessSync
{
    public static class AccessCapabilityResolver
    {
        public const string ReadVerb = "ver";
        public const string WriteVerb = "editar";
        public const string DeleteVerb = "excluir";

        public static IReadOnlyList<string> Resolve(Type controllerType, MethodInfo action, string httpMethod)
        {
            ArgumentNullException.ThrowIfNull(controllerType);
            ArgumentNullException.ThrowIfNull(action);

            AccessCapabilityAttribute? explicitCapability = action.GetCustomAttribute<AccessCapabilityAttribute>(inherit: true)
                ?? controllerType.GetCustomAttribute<AccessCapabilityAttribute>(inherit: true);

            if (explicitCapability is not null)
            {
                return explicitCapability.Keys;
            }

            AccessModuleAttribute? module = controllerType.GetCustomAttribute<AccessModuleAttribute>(inherit: true);
            if (module is null)
            {
                return [];
            }

            string verb = InferVerb(httpMethod);
            List<string> keys = [$"{module.Module}.{verb}"];

            if (verb == ReadVerb)
            {
                keys.AddRange(module.SharedRead
                    .Where(shared => !string.IsNullOrWhiteSpace(shared))
                    .Select(shared => $"{shared.Trim()}.{ReadVerb}")
                    .Where(key => !keys.Contains(key, StringComparer.OrdinalIgnoreCase)));
            }

            return keys;
        }

        public static string InferVerb(string? httpMethod)
        {
            return (httpMethod ?? string.Empty).Trim().ToUpperInvariant() switch
            {
                "GET" or "HEAD" or "OPTIONS" => ReadVerb,
                "DELETE" => DeleteVerb,
                _ => WriteVerb
            };
        }

        public static string ModuleOf(string key)
        {
            int separator = key.IndexOf('.');
            return separator > 0 ? key[..separator] : key;
        }

        public static string VerbOf(string key)
        {
            int separator = key.IndexOf('.');
            return separator > 0 && separator < key.Length - 1 ? key[(separator + 1)..] : string.Empty;
        }

        /// <summary>
        /// Monta o catalogo (um item por capacidade distinta dos recursos) com rotulos, ordem e
        /// marcacao de capacidade basica. <paramref name="translate"/> devolve null quando a chave
        /// de traducao nao existe.
        /// </summary>
        public static List<AccessCapabilityModel> BuildCatalog(IEnumerable<AccessResourceModel> resources, AccessCatalogAttribute? catalog, Func<string, string?> translate)
        {
            ArgumentNullException.ThrowIfNull(resources);
            ArgumentNullException.ThrowIfNull(translate);

            List<string> modules = (catalog?.Modules ?? []).Select(item => item.Trim()).ToList();
            List<string> verbs = (catalog?.Verbs ?? []).Select(item => item.Trim()).ToList();
            HashSet<string> baseline = (catalog?.Baseline ?? []).Select(item => item.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<string> keys = resources
                .SelectMany(resource => resource.Capabilities)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<string> orderedModules = keys
                .Select(ModuleOf)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(module => RankOf(modules, module))
                .ThenBy(module => module, StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<AccessCapabilityModel> result = [];

            foreach (string module in orderedModules)
            {
                int moduleOrder = orderedModules.IndexOf(module) + 1;
                string moduleLabel = translate($"accessModule.{module}") ?? Capitalize(module);

                List<string> moduleKeys = keys
                    .Where(key => string.Equals(ModuleOf(key), module, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(key => RankOf(verbs, VerbOf(key)))
                    .ThenBy(key => VerbOf(key), StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (string key in moduleKeys)
                {
                    result.Add(new AccessCapabilityModel
                    {
                        Key = key,
                        Module = module,
                        ModuleLabel = moduleLabel,
                        ModuleOrder = moduleOrder,
                        Label = translate($"accessCapability.{key}") ?? Capitalize(VerbOf(key)),
                        Description = translate($"accessCapability.{key}.description") ?? string.Empty,
                        Order = moduleKeys.IndexOf(key) + 1,
                        IsBaseline = baseline.Contains(key)
                    });
                }
            }

            return result;
        }

        private static int RankOf(List<string> ordered, string value)
        {
            int index = ordered.FindIndex(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
            return index < 0 ? int.MaxValue : index;
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return char.ToUpperInvariant(value[0]) + value[1..];
        }
    }
}
