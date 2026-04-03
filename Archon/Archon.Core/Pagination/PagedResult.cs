namespace Archon.Core.Pagination
{
    public sealed class PagedResult<T>
    {
        public IReadOnlyCollection<T> Items { get; init; } = Array.Empty<T>();

        public PaginationMetadata Pagination { get; init; } = new PaginationMetadata();
    }
}
