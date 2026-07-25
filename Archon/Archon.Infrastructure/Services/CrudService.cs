using Archon.Application.Services;
using Archon.Core.Entities;
using Archon.Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.ComponentModel.DataAnnotations;
using ValidationException = System.ComponentModel.DataAnnotations.ValidationException;

namespace Archon.Infrastructure.Services
{
    public class CrudService<T> : ICrudService<T> where T : Entity
    {
        protected readonly DbContext DbContext;
        private readonly List<Exception> messages = [];

        public CrudService(DbContext dbContext)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            DbContext = dbContext;
        }

        public IReadOnlyCollection<Exception> Messages => messages.AsReadOnly();

        protected ICollection<Exception> MutableMessages => messages;

        public virtual bool CustomValidate(T entity)
        {
            return true;
        }

        public virtual string GetErrorMessages()
        {
            return string.Join(" | ", messages.Select(exception => exception.Message));
        }

        public virtual bool Validate(T entity)
        {
            messages.Clear();
            return ValidateEntity(entity);
        }

        public virtual async Task<bool> ExecuteInTransaction(Func<Task> operation, CancellationToken cancellationToken = default)
        {
            try
            {
                IExecutionStrategy executionStrategy = DbContext.Database.CreateExecutionStrategy();
                await executionStrategy.ExecuteAsync(async () =>
                {
                    await using IDbContextTransaction transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);
                    await operation();
                    await DbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                });

                return true;
            }
            catch (Exception exception)
            {
                if (IsIntegrityViolation(exception))
                {
                    throw new IntegrityException();
                }

                // Apenas o controle de fluxo ESPERADO vira mensagem. `KeyNotFoundException` e lancada
                // pelo proprio Update quando o registro some entre a checagem e a transacao, e
                // `ApiControllerCrud.ResolveServiceError` depende dela para devolver 404. Excecao de
                // dominio sobe para o middleware, que ja sabe mapear cada tipo no status certo.
                if (exception is KeyNotFoundException)
                {
                    messages.Add(exception);
                    return false;
                }

                // Qualquer outra coisa e falha de verdade: timeout, conexao caida, bug. Antes daqui
                // tudo virava `return false` e desaparecia se o chamador nao checasse o retorno.
                throw;
            }
            finally
            {
                DbContext.ChangeTracker.Clear();
            }
        }

        public virtual async Task<bool> Insert(CancellationToken cancellationToken = default, params T[] entities)
        {
            messages.Clear();

            if (entities.Length == 0)
            {
                return true;
            }

            bool isValid = true;
            foreach (T entity in entities)
            {
                if (!ValidateEntity(entity))
                {
                    isValid = false;
                }
            }

            if (!isValid)
            {
                return false;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (T entity in entities)
            {
                entity.SetCreatedAt(now);
            }

            return await ExecuteInTransaction(async () =>
            {
                DbContext.Set<T>().AddRange(entities);
                await Task.CompletedTask;
            }, cancellationToken);
        }

        public virtual async Task<T?> Update(T entity, CancellationToken cancellationToken = default)
        {
            messages.Clear();

            if (!ValidateEntity(entity))
            {
                return null;
            }

            T? existingEntity = await DbContext.Set<T>()
                .AsNoTracking()
                .FirstOrDefaultAsync(current => current.Id == entity.Id, cancellationToken);

            if (existingEntity is null)
            {
                messages.Add(new KeyNotFoundException("record.notFound"));
                return null;
            }

            return await ExecuteInTransaction(async () =>
            {
                T? currentEntity = await DbContext.Set<T>()
                    .AsTracking()
                    .FirstOrDefaultAsync(current => current.Id == entity.Id, cancellationToken);

                if (currentEntity is null)
                {
                    throw new KeyNotFoundException("record.notFound");
                }

                DateTimeOffset createdAt = currentEntity.CreatedAt;
                DateTimeOffset updatedAt = DateTimeOffset.UtcNow;
                DbContext.Entry(currentEntity).CurrentValues.SetValues(entity);
                currentEntity.SetCreatedAt(createdAt);
                currentEntity.SetUpdatedAt(updatedAt);
                entity.SetCreatedAt(createdAt);
                entity.SetUpdatedAt(updatedAt);
            }, cancellationToken) ? entity : null;
        }

        public virtual async Task<T?> Delete(long id, CancellationToken cancellationToken = default)
        {
            messages.Clear();

            // Reaproveita a instancia ja rastreada quando existir. Buscar sempre AsNoTracking e mandar
            // RemoveRange obriga o EF a anexar uma SEGUNDA instancia da mesma chave, e ele recusa com
            // "another instance with the same key value is already being tracked". Enquanto o
            // ExecuteInTransaction engolia excecao isso virava um delete que falhava em silencio.
            T? entity = DbContext.ChangeTracker.Entries<T>()
                .FirstOrDefault(entry => entry.Entity.Id == id)?.Entity;

            entity ??= await DbContext.Set<T>()
                .AsNoTracking()
                .FirstOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (entity is null)
            {
                messages.Add(new KeyNotFoundException("record.notFound"));
                return null;
            }

            return await Delete([entity], cancellationToken) ? entity : null;
        }

        public virtual async Task<bool> Delete(T[] entities, CancellationToken cancellationToken = default)
        {
            messages.Clear();

            if (entities.Length == 0)
            {
                return true;
            }

            return await ExecuteInTransaction(async () =>
            {
                DbContext.Set<T>().RemoveRange(entities);
                await Task.CompletedTask;
            }, cancellationToken);
        }

        protected virtual bool ValidateEntity(T entity)
        {
            if (entity is null)
            {
                messages.Add(new ArgumentNullException(nameof(entity), "request.body.required"));
                return false;
            }

            ValidationContext validationContext = new ValidationContext(entity);
            List<ValidationResult> validationResults = [];
            bool isValid = Validator.TryValidateObject(entity, validationContext, validationResults, true);

            if (!isValid)
            {
                foreach (ValidationResult validationResult in validationResults)
                {
                    messages.Add(new ValidationException(validationResult.ErrorMessage));
                }

                return false;
            }

            return CustomValidate(entity);
        }

        protected virtual bool IsIntegrityViolation(Exception exception)
        {
            return exception is DbUpdateException or DbUpdateConcurrencyException;
        }
    }
}
