using Archon.Application.Services;
using Archon.Core.Bulk;
using Archon.Core.Entities;
using Archon.Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Archon.Infrastructure.Services
{
    public sealed class BulkOperationRunner : IBulkOperationRunner
    {
        private const string NotFoundKey = "record.notFound";
        private const string IntegrityKey = "error.integrity.violation";
        private const string UnexpectedKey = "error.unexpected.short";

        private readonly DbContext dbContext;
        private readonly ILogger<BulkOperationRunner> logger;

        public BulkOperationRunner(DbContext dbContext, ILogger<BulkOperationRunner> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BulkOperationResult> RunAsync(IReadOnlyCollection<long> ids, Func<long, CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(ids);
            ArgumentNullException.ThrowIfNull(operation);

            List<long> distinctIds = ids.Distinct().ToList();
            BulkOperationResult result = new(distinctIds.Count);

            foreach (long id in distinctIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await operation(id, cancellationToken);
                    result.AddSuccess(id);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    RecordFailure(result, id, exception);

                    // O que ficou pendente no rastreador (entidade alterada que nao gravou) seria gravado de novo
                    // pelo SaveChanges do proximo registro, e a falha de um contaminaria os seguintes.
                    dbContext.ChangeTracker.Clear();
                }
            }

            return result;
        }

        public Task<BulkOperationResult> SetActiveAsync<T>(IReadOnlyCollection<long> ids, bool isActive, Func<T, CancellationToken, Task>? validate = null, CancellationToken cancellationToken = default)
            where T : Entity, IActivatable
        {
            return RunAsync(ids, async (id, token) =>
            {
                T entity = await dbContext.Set<T>()
                    .AsTracking()
                    .FirstOrDefaultAsync(item => item.Id == id, token)
                    ?? throw new NotFoundException(NotFoundKey);

                if (entity.IsActive == isActive)
                {
                    return;
                }

                if (validate is not null)
                {
                    await validate(entity, token);
                }

                if (isActive)
                {
                    entity.Activate();
                }
                else
                {
                    entity.Deactivate();
                }

                await dbContext.SaveChangesAsync(token);
            }, cancellationToken);
        }

        private void RecordFailure(BulkOperationResult result, long id, Exception exception)
        {
            switch (exception)
            {
                case DomainException domainException:
                    result.AddFailure(id, domainException.Message, domainException.MessageArgs);
                    break;
                case KeyNotFoundException or ArgumentException or InvalidOperationException or IntegrityException when !string.IsNullOrWhiteSpace(exception.Message):
                    // Chave i18n crua por convencao. Se nao for chave, a camada HTTP troca pela mensagem generica.
                    result.AddFailure(id, exception.Message);
                    break;
                case DbUpdateException:
                    logger.LogWarning(exception, "Operacao em lote: violacao de integridade no registro {Id}.", id);
                    result.AddFailure(id, IntegrityKey);
                    break;
                default:
                    logger.LogError(exception, "Operacao em lote: falha inesperada no registro {Id}.", id);
                    result.AddFailure(id, UnexpectedKey);
                    break;
            }
        }
    }
}
