namespace Archon.Core.Pagination
{
    public sealed class PaginationMetadata
    {
        public int Page { get; init; }

        public int PageSize { get; init; }

        public long TotalCount { get; init; }

        public int TotalPages { get; init; }

        public bool HasPreviousPage => Page > 1;

        public bool HasNextPage => Page < TotalPages;
    }
}
