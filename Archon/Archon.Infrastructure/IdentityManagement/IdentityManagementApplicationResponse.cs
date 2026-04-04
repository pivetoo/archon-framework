namespace Archon.Infrastructure.IdentityManagement
{
    public sealed class IdentityManagementApplicationResponse
    {
        public string Message { get; init; } = string.Empty;

        public IdentityManagementApplicationInfo? Data { get; init; }

        public object? Errors { get; init; }

        public object? Pagination { get; init; }
    }
}
