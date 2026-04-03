namespace Archon.Application.Abstractions
{
    public interface ICurrentUser
    {
        bool IsAuthenticated { get; }

        long? UserId { get; }

        string? UserName { get; }

        string? Email { get; }

        string? ClientId { get; }
    }
}
