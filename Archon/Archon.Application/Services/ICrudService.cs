using Archon.Core.Entities;

namespace Archon.Application.Services
{
    public interface ICrudService<T> where T : Entity
    {
        IReadOnlyCollection<Exception> Messages { get; }

        string GetErrorMessages();

        bool Validate(T entity);

        bool CustomValidate(T entity);

        bool ExecuteInTransaction(Action operation);

        bool Insert(params T[] entities);

        T? Update(T entity);

        T? Delete(long id);

        bool Delete(params T[] entities);
    }
}
