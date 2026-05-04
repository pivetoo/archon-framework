namespace Archon.Infrastructure.RestApi
{
    public sealed class RestResponse<T>
    {
        public int Status { get; init; }

        public T? Data { get; init; }

        public List<string> Errors { get; init; } = [];

        public bool Ok => Status is >= 200 and < 300;
    }
}
