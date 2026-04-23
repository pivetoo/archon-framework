using System.Text.Json.Serialization;

namespace Archon.Core.Responses
{
    public class ApiResponse<T>
    {
        public string Message { get; init; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public T? Data { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Errors { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Pagination { get; init; }
    }

    public sealed class ApiResponse : ApiResponse<object>
    {
    }
}
