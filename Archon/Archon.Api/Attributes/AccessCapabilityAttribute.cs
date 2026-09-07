namespace Archon.Api.Attributes
{
    /// <summary>
    /// Capacidades explicitas de uma action (ou de todas as actions do controller). Qualquer uma delas
    /// libera o endpoint. Tem precedencia sobre a inferencia de <see cref="AccessModuleAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class AccessCapabilityAttribute : Attribute
    {
        public IReadOnlyList<string> Keys { get; }

        public AccessCapabilityAttribute(params string[] keys)
        {
            Keys = keys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (Keys.Count == 0)
            {
                throw new ArgumentException("At least one capability key is required.", nameof(keys));
            }
        }
    }
}
