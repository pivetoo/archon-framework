using Archon.Core.Entities;

namespace Archon.Application.Services
{
    public interface ICrudService<T> where T : Entity
    {
        IReadOnlyCollection<Exception> Messages { get; }

        string GetErrorMessages();

        bool Validate(T entity);

        bool CustomValidate(T entity);

        Task<bool> ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);

        Task<bool> InsertAsync(CancellationToken cancellationToken = default, params T[] entities);

        Task<T?> UpdateAsync(T entity, CancellationToken cancellationToken = default);

        Task<T?> DeleteAsync(long id, CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(T[] entities, CancellationToken cancellationToken = default);
    }
}
