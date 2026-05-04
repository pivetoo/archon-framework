using System.Net.Http.Json;
using System.Text.Json;

namespace Archon.Infrastructure.RestApi
{
    public sealed class RestApi
    {
        private readonly HttpClient http;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public RestApi(HttpClient http)
        {
            this.http = http;
        }

        public async Task<RestResponse<T>> Fetch<T>(RestRequest request, CancellationToken ct = default)
        {
            using HttpRequestMessage message = new(request.Method, request.Url);

            foreach (KeyValuePair<string, string> header in request.Headers)
                message.Headers.TryAddWithoutValidation(header.Key, header.Value);

            if (request.Body is not null)
                message.Content = JsonContent.Create(request.Body, options: JsonOptions);

            HttpResponseMessage response = await http.SendAsync(message, ct);
            string body = await response.Content.ReadAsStringAsync(ct);

            return new RestResponse<T>
            {
                Status = (int)response.StatusCode,
                Data = response.IsSuccessStatusCode ? Deserialize<T>(body) : default,
                Errors = response.IsSuccessStatusCode ? [] : ParseErrors(body)
            };
        }

        public async Task<RestResponse<string>> FetchString(RestRequest request, CancellationToken ct = default)
        {
            using HttpRequestMessage message = new(request.Method, request.Url);

            foreach (KeyValuePair<string, string> header in request.Headers)
                message.Headers.TryAddWithoutValidation(header.Key, header.Value);

            if (request.Body is not null)
                message.Content = JsonContent.Create(request.Body, options: JsonOptions);

            HttpResponseMessage response = await http.SendAsync(message, ct);
            string body = await response.Content.ReadAsStringAsync(ct);

            return new RestResponse<string>
            {
                Status = (int)response.StatusCode,
                Data = response.IsSuccessStatusCode ? body : default,
                Errors = response.IsSuccessStatusCode ? [] : ParseErrors(body)
            };
        }

        private static T? Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }

        private static List<string> ParseErrors(string body)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                return doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("errors", out JsonElement errors)
                    && errors.ValueKind == JsonValueKind.Array
                    ? errors.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList()
                    : [body];
            }
            catch
            {
                return [body];
            }
        }
    }
}
