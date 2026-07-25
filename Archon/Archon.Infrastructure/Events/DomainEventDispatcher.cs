using Archon.Application.Events;
using Archon.Core.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Archon.Infrastructure.Events
{
    public sealed class DomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<DomainEventDispatcher> logger;

        public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
        }

        public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            await DispatchAsync([domainEvent], cancellationToken);
        }

        public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            foreach (IDomainEvent domainEvent in domainEvents)
            {
                Type eventType = domainEvent.GetType();
                Type handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
                IEnumerable<object?> handlers = serviceProvider.GetServices(handlerType);

                foreach (object? handler in handlers)
                {
                    if (handler is null)
                    {
                        continue;
                    }

                    try
                    {
                        Task task = (Task)handlerType.GetMethod("HandleAsync")!
                            .Invoke(handler, [domainEvent, cancellationToken])!;

                        await task;
                    }
                    catch (TargetInvocationException exception) when (exception.InnerException is not null)
                    {
                        // O despacho e por reflexao: se o handler lanca ANTES de devolver a Task, o
                        // Invoke embrulha a excecao real num TargetInvocationException e o log mostrava
                        // o embrulho em vez da causa. Desembrulha para o erro chegar util no log.
                        logger.LogError(
                            exception.InnerException,
                            "Error handling domain event {EventType}. Handler: {HandlerType}",
                            eventType.Name,
                            handler.GetType().Name);
                    }
                    catch (Exception exception)
                    {
                        // Handler que falha NAO derruba o comando: o evento e consequencia, nao parte da
                        // transacao. Fica registrado como erro, mas nao ha retry nem fila de mortos —
                        // handler que precise garantir entrega tem que tratar isso por conta propria.
                        logger.LogError(
                            exception,
                            "Error handling domain event {EventType}. Handler: {HandlerType}",
                            eventType.Name,
                            handler.GetType().Name);
                    }
                }
            }
        }
    }
}
