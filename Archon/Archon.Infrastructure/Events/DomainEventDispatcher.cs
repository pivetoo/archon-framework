using Archon.Application.Events;
using Archon.Core.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
                    catch (Exception exception)
                    {
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
