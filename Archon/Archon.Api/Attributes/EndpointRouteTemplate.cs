namespace Archon.Api.Attributes
{
    internal static class EndpointRouteTemplate
    {
        public static string Normalize(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return template;
            }

            return template.StartsWith('{') ? $"[action]/{template}" : template;
        }
    }
}
