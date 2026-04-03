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

        public virtual bool ExecuteInTransaction(Action operation)
        {
            try
            {
                IExecutionStrategy executionStrategy = DbContext.Database.CreateExecutionStrategy();
                executionStrategy.Execute(() =>
                {
                    using IDbContextTransaction transaction = DbContext.Database.BeginTransaction();
                    operation();
                    DbContext.SaveChanges();
                    transaction.Commit();
                });

                return true;
            }
            catch (Exception exception)
            {
                if (IsIntegrityViolation(exception))
                {
                    throw new IntegrityException();
                }

                messages.Add(exception);
                return false;
            }
            finally
            {
                DbContext.ChangeTracker.Clear();
            }
        }

        public virtual bool Insert(params T[] entities)
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

            return ExecuteInTransaction(() =>
            {
                DbContext.Set<T>().AddRange(entities);
            });
        }

        public virtual T? Update(T entity)
        {
            messages.Clear();

            if (!ValidateEntity(entity))
            {
                return null;
            }

            T? existingEntity = DbContext.Set<T>()
                .AsNoTracking()
                .FirstOrDefault(current => current.Id == entity.Id);

            if (existingEntity is null)
            {
                messages.Add(new KeyNotFoundException("Record not found."));
                return null;
            }

            return ExecuteInTransaction(() =>
            {
                T? currentEntity = DbContext.Set<T>().AsTracking().FirstOrDefault(current => current.Id == entity.Id);
                if (currentEntity is null)
                {
                    throw new KeyNotFoundException("Record not found.");
                }

                DateTimeOffset createdAt = currentEntity.CreatedAt;
                DateTimeOffset updatedAt = DateTimeOffset.UtcNow;
                DbContext.Entry(currentEntity).CurrentValues.SetValues(entity);
                currentEntity.SetCreatedAt(createdAt);
                currentEntity.SetUpdatedAt(updatedAt);
                entity.SetCreatedAt(createdAt);
                entity.SetUpdatedAt(updatedAt);
            }) ? entity : null;
        }

        public virtual T? Delete(long id)
        {
            messages.Clear();
            T? entity = DbContext.Set<T>().AsNoTracking().FirstOrDefault(current => current.Id == id);
            if (entity is null)
            {
                messages.Add(new KeyNotFoundException("Record not found."));
                return null;
            }

            return Delete(entity) ? entity : null;
        }

        public virtual bool Delete(params T[] entities)
        {
            messages.Clear();

            if (entities.Length == 0)
            {
                return true;
            }

            return ExecuteInTransaction(() =>
            {
                DbContext.Set<T>().RemoveRange(entities);
            });
        }

        protected virtual bool ValidateEntity(T entity)
        {
            if (entity is null)
            {
                messages.Add(new ArgumentNullException(nameof(entity), "Request body cannot be null."));
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
