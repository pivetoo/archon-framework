using Archon.Application.Events;
using Archon.Core.Events;
using Archon.Infrastructure.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Archon.Testing.Unit.Infrastructure.Events
{
    public sealed class DomainEventDispatcherTests
    {
        [Test]
        public async Task DispatchAsync_ShouldCallHandler()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<IDomainEventHandler<TestEvent>, TestEventHandler>();
            services.AddLogging();
            ServiceProvider provider = services.BuildServiceProvider();

            ILogger<DomainEventDispatcher> logger = provider.GetRequiredService<ILogger<DomainEventDispatcher>>();
            DomainEventDispatcher dispatcher = new(provider, logger);
            TestEvent domainEvent = new TestEvent();

            await dispatcher.DispatchAsync(domainEvent);

            TestEventHandler handler = (TestEventHandler)provider.GetRequiredService<IDomainEventHandler<TestEvent>>();
            Assert.That(handler.HandledEvents.Count, Is.EqualTo(1));
            Assert.That(handler.HandledEvents[0], Is.SameAs(domainEvent));
        }

        [Test]
        public async Task DispatchAsync_ShouldCallMultipleHandlers()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<IDomainEventHandler<TestEvent>, TestEventHandler>();
            services.AddSingleton<IDomainEventHandler<TestEvent>, AnotherTestEventHandler>();
            services.AddLogging();
            ServiceProvider provider = services.BuildServiceProvider();

            ILogger<DomainEventDispatcher> logger = provider.GetRequiredService<ILogger<DomainEventDispatcher>>();
            DomainEventDispatcher dispatcher = new(provider, logger);
            TestEvent domainEvent = new TestEvent();

            await dispatcher.DispatchAsync(domainEvent);

            Assert.That(provider.GetServices<IDomainEventHandler<TestEvent>>().Count(), Is.EqualTo(2));
        }

        [Test]
        public async Task DispatchAsync_ShouldNotFail_WhenNoHandlers()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddLogging();
            ServiceProvider provider = services.BuildServiceProvider();

            ILogger<DomainEventDispatcher> logger = provider.GetRequiredService<ILogger<DomainEventDispatcher>>();
            DomainEventDispatcher dispatcher = new(provider, logger);
            TestEvent domainEvent = new TestEvent();

            Assert.DoesNotThrowAsync(async () => await dispatcher.DispatchAsync(domainEvent));
        }

        [Test]
        public async Task DispatchAsync_ShouldNotFail_WhenHandlerThrows()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<IDomainEventHandler<TestEvent>, FailingTestEventHandler>();
            services.AddLogging();
            ServiceProvider provider = services.BuildServiceProvider();

            ILogger<DomainEventDispatcher> logger = provider.GetRequiredService<ILogger<DomainEventDispatcher>>();
            DomainEventDispatcher dispatcher = new(provider, logger);
            TestEvent domainEvent = new TestEvent();

            Assert.DoesNotThrowAsync(async () => await dispatcher.DispatchAsync(domainEvent));
        }

        [Test]
        public async Task DispatchAsync_ShouldDispatchMultipleEvents()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<IDomainEventHandler<TestEvent>, TestEventHandler>();
            services.AddLogging();
            ServiceProvider provider = services.BuildServiceProvider();

            ILogger<DomainEventDispatcher> logger = provider.GetRequiredService<ILogger<DomainEventDispatcher>>();
            DomainEventDispatcher dispatcher = new(provider, logger);
            TestEvent event1 = new TestEvent();
            TestEvent event2 = new TestEvent();

            await dispatcher.DispatchAsync([event1, event2]);

            TestEventHandler handler = (TestEventHandler)provider.GetRequiredService<IDomainEventHandler<TestEvent>>();
            Assert.That(handler.HandledEvents.Count, Is.EqualTo(2));
        }

        private class TestEvent : IDomainEvent
        {
            public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
        }

        private class TestEventHandler : IDomainEventHandler<TestEvent>
        {
            public List<TestEvent> HandledEvents { get; } = [];

            public Task HandleAsync(TestEvent domainEvent, CancellationToken cancellationToken = default)
            {
                HandledEvents.Add(domainEvent);
                return Task.CompletedTask;
            }
        }

        private class AnotherTestEventHandler : IDomainEventHandler<TestEvent>
        {
            public Task HandleAsync(TestEvent domainEvent, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        private class FailingTestEventHandler : IDomainEventHandler<TestEvent>
        {
            public Task HandleAsync(TestEvent domainEvent, CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("Handler failure");
            }
        }
    }
}
