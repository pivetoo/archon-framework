using Archon.Core.Entities;

namespace Archon.Application.Services
{
    public interface ICrudService<T> where T : Entity
    {
        IReadOnlyCollection<Exception> Messages { get; }

        string GetErrorMessages();

        bool Validate(T entity);

        bool CustomValidate(T entity);

        Task<bool> ExecuteInTransaction(Func<Task> operation, CancellationToken cancellationToken = default);

        Task<bool> Insert(CancellationToken cancellationToken = default, params T[] entities);

        Task<T?> Update(T entity, CancellationToken cancellationToken = default);

        Task<T?> Delete(long id, CancellationToken cancellationToken = default);

        Task<bool> Delete(T[] entities, CancellationToken cancellationToken = default);
    }
}
