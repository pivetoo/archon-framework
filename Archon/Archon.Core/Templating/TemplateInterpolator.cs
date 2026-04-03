using System.Text.Json;
using System.Text.RegularExpressions;

namespace Archon.Core.Templating
{
    public static class TemplateInterpolator
    {
        private static readonly Regex TemplateRegex = new(@"\{\{(\s*[\w.\-]+\s*)\}\}", RegexOptions.Compiled);

        public static string Interpolate(
            string? template,
            Dictionary<string, object> stepVariables,
            Dictionary<string, object> payloadData,
            Dictionary<string, string> connectorAttributes)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            return TemplateRegex.Replace(template, match =>
            {
                string variable = match.Groups[1].Value.Trim();
                return ResolveVariable(variable, stepVariables, payloadData, connectorAttributes);
            });
        }

        private static string ResolveVariable(
            string variable,
            Dictionary<string, object> stepVariables,
            Dictionary<string, object> payloadData,
            Dictionary<string, string> connectorAttributes)
        {
            if (stepVariables.TryGetValue(variable, out object? stepValue) ||
                TryGetValueIgnoreCase(stepVariables, variable, out stepValue))
            {
                return ConvertToString(stepValue);
            }

            object? payloadValue = ResolveDottedPath(payloadData, variable);
            if (payloadValue is not null)
            {
                return ConvertToString(payloadValue);
            }

            if (connectorAttributes.TryGetValue(variable, out string? attributeValue) ||
                TryGetValueIgnoreCase(connectorAttributes, variable, out attributeValue))
            {
                return attributeValue ?? string.Empty;
            }

            return string.Empty;
        }

        private static object? ResolveDottedPath(Dictionary<string, object> data, string path)
        {
            string[] parts = path.Split('.');
            object? current = data;

            foreach (string part in parts)
            {
                if (current is null)
                {
                    return null;
                }

                if (current is Dictionary<string, object> dictionary)
                {
                    if (!dictionary.TryGetValue(part, out current))
                    {
                        return null;
                    }
                }
                else if (current is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == JsonValueKind.Object &&
                        jsonElement.TryGetProperty(part, out JsonElement property))
                    {
                        current = property;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }

            return current;
        }

        private static bool TryGetValueIgnoreCase<T>(Dictionary<string, T> source, string key, out T? value)
        {
            foreach ((string sourceKey, T sourceValue) in source)
            {
                if (string.Equals(sourceKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = sourceValue;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static string ConvertToString(object? value)
        {
            if (value is null)
            {
                return string.Empty;
            }

            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.String => jsonElement.GetString() ?? string.Empty,
                    JsonValueKind.Number => jsonElement.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => string.Empty,
                    _ => jsonElement.GetRawText()
                };
            }

            return value.ToString() ?? string.Empty;
        }
    }
}
