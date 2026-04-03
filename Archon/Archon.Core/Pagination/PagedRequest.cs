namespace Archon.Core.Pagination
{
    public sealed class PagedRequest
    {
        private const int DefaultPage = 1;
        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 200;

        private int page = DefaultPage;
        private int pageSize = DefaultPageSize;

        public int Page
        {
            get => page;
            set => page = value < 1 ? DefaultPage : value;
        }

        public int PageSize
        {
            get => pageSize;
            set => pageSize = value switch
            {
                < 1 => DefaultPageSize,
                > MaxPageSize => MaxPageSize,
                _ => value
            };
        }
    }
}
