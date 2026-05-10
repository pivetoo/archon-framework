using System.Text.Json;

namespace Archon.Infrastructure.RestApi
{
    public sealed class RestRequest
    {
        public string Url { get; }
        public HttpMethod Method { get; private set; } = HttpMethod.Get;
        public object? Body { get; private set; }
        public Dictionary<string, string> Headers { get; } = [];

        private RestRequest(string url)
        {
            Url = url;
        }

        public static RestRequest Get(string url) => new(url);

        public static RestRequest Post(string url, object body) => new(url) { Method = HttpMethod.Post, Body = body };

        public static RestRequest Put(string url, object body) => new(url) { Method = HttpMethod.Put, Body = body };

        public static RestRequest Delete(string url) => new(url) { Method = HttpMethod.Delete };

        public RestRequest WithHeader(string key, string value)
        {
            Headers[key] = value;
            return this;
        }

        public RestRequest WithApiKey(string apiKey)
            => WithHeader("X-Api-Key", apiKey);

        [Obsolete("Use WithApiKey. Mantido apenas para compatibilidade durante a transicao.")]
        public RestRequest WithSecret(string secret)
            => WithApiKey(secret);
    }
}
