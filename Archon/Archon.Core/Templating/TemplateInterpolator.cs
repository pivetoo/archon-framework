using System.Text.Json;
using System.Text.RegularExpressions;

namespace Archon.Core.Templating
{
    public static class TemplateInterpolator
    {
        private static readonly Regex TemplateRegex = new(@"\{\{(\s*[\w.\-]+(?:\s*\|\s*\w+)?\s*)\}\}", RegexOptions.Compiled);
        private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

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
                string expression = match.Groups[1].Value.Trim();
                (string variable, string? filter) = ParseExpression(expression);
                return ResolveVariable(variable, filter, stepVariables, payloadData, connectorAttributes);
            });
        }

        private static (string variable, string? filter) ParseExpression(string expression)
        {
            int pipeIndex = expression.IndexOf('|');
            if (pipeIndex < 0)
            {
                return (expression.Trim(), null);
            }

            string variable = expression[..pipeIndex].Trim();
            string filter = expression[(pipeIndex + 1)..].Trim();
            return (variable, filter);
        }

        private static string ResolveVariable(
            string variable,
            string? filter,
            Dictionary<string, object> stepVariables,
            Dictionary<string, object> payloadData,
            Dictionary<string, string> connectorAttributes)
        {
            if (stepVariables.TryGetValue(variable, out object? stepValue) ||
                TryGetValueIgnoreCase(stepVariables, variable, out stepValue))
            {
                return ApplyFilter(stepValue, filter);
            }

            object? payloadValue = ResolveDottedPath(payloadData, variable);
            if (payloadValue is not null)
            {
                return ApplyFilter(payloadValue, filter);
            }

            if (connectorAttributes.TryGetValue(variable, out string? attributeValue) ||
                TryGetValueIgnoreCase(connectorAttributes, variable, out attributeValue))
            {
                return ApplyFilter(attributeValue, filter);
            }

            return filter == "json" ? "null" : string.Empty;
        }

        private static string ApplyFilter(object? value, string? filter)
        {
            if (filter == "json")
            {
                return JsonSerializer.Serialize(value, JsonOptions);
            }

            return ConvertToString(value);
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
                    else if (jsonElement.ValueKind == JsonValueKind.Array && int.TryParse(part, out int index) && index >= 0 && index < jsonElement.GetArrayLength())
                    {
                        current = jsonElement[index];
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
