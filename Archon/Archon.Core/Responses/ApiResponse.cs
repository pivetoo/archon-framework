using System.Text.Json.Serialization;

namespace Archon.Core.Responses
{
    public sealed class ApiResponse
    {
        public string Message { get; init; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Data { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Errors { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Pagination { get; init; }
    }
}
