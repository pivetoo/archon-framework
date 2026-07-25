using Archon.Core.Entities;
using Archon.Core.Events;
using Archon.Application.Abstractions;
using Archon.Application.Events;
using Archon.Application.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Reflection;

namespace Archon.Infrastructure.Persistence.EF
{
    public class ArchonDbContext : DbContext
    {
        private readonly IReadOnlyCollection<Assembly> modelAssemblies;
        private readonly ArchonAuditManager auditManager;
        private readonly IDomainEventDispatcher? domainEventDispatcher;
        private readonly string? schema;
        private bool isAuditing;

        public ArchonDbContext(
            DbContextOptions<ArchonDbContext> options,
            ModelAssemblyRegistry modelAssemblyRegistry,
            ICurrentUser? currentUser = null,
            ITenantContext? tenantContext = null,
            IDomainEventDispatcher? domainEventDispatcher = null,
            string? schema = null) : base(options)
        {
            modelAssemblies = modelAssemblyRegistry.Assemblies;
            auditManager = new ArchonAuditManager(ChangeTracker, currentUser, tenantContext);
            this.domainEventDispatcher = domainEventDispatcher;
            this.schema = schema;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (!string.IsNullOrWhiteSpace(schema))
            {
                modelBuilder.HasDefaultSchema(schema);
            }

            List<Type> entityTypes = modelAssemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type =>
                    type.IsClass &&
                    !type.IsAbstract &&
                    typeof(Entity).IsAssignableFrom(type))
                .ToList();

            foreach (Type entityType in entityTypes)
            {
                modelBuilder.Entity(entityType);
            }

            ArchonModelConventions.Apply(modelBuilder);

            foreach (Assembly assembly in modelAssemblies)
            {
                modelBuilder.ApplyConfigurationsFromAssembly(assembly);
            }

            ArchonModelConventions.ApplyIdentifierConventions(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            return SaveChangesAsync(acceptAllChangesOnSuccess, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override int SaveChanges()
        {
            return SaveChanges(true);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return SaveChangesAsync(true, cancellationToken);
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            if (isAuditing)
            {
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }

            List<IDomainEvent> domainEvents = CollectDomainEvents();
            auditManager.ApplyEntityTimestamps();

            // Captura ANTES do save: estado, valores originais e propriedades modificadas so existem
            // enquanto o ChangeTracker nao foi aceito. Materializacao DEPOIS: o Id de entidade inserida
            // e gerado pelo banco no save. Fazer as duas coisas juntas gravava EntityId "0" em toda
            // auditoria de insercao.
            List<ArchonAuditManager.PendingAuditEntry> pendingAuditEntries = auditManager.CapturePendingAuditEntries();

            // A auditoria precisa de um SEGUNDO save (o Id da entidade inserida so existe apos o
            // primeiro). Sem transacao envolvendo os dois, falha ao gravar a auditoria deixava a
            // mudanca aplicada e sem registro. Abre transacao propria so quando o chamador ainda nao
            // abriu uma — no caminho do CrudService ela ja existe. Provider nao relacional (InMemory,
            // usado nos testes) nao suporta transacao, entao fica de fora.
            bool precisaDeTransacaoPropria = pendingAuditEntries.Count > 0
                && Database.CurrentTransaction is null
                && Database.IsRelational();

            IDbContextTransaction? transacao = precisaDeTransacaoPropria
                ? await Database.BeginTransactionAsync(cancellationToken)
                : null;

            int result;

            try
            {
                result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

                List<AuditEntry> auditEntries = ArchonAuditManager.MaterializeAuditEntries(pendingAuditEntries);

                if (auditEntries.Count > 0)
                {
                    await PersistAuditEntriesAsync(auditEntries, cancellationToken);
                }

                if (transacao is not null)
                {
                    await transacao.CommitAsync(cancellationToken);
                }
            }
            catch
            {
                if (transacao is not null)
                {
                    await transacao.RollbackAsync(cancellationToken);
                }

                throw;
            }
            finally
            {
                if (transacao is not null)
                {
                    await transacao.DisposeAsync();
                }
            }

            // Fora da transacao de proposito: handler de evento nao deve poder reverter a escrita ja
            // confirmada, e eventos sao despachados depois do commit para verem estado consistente.
            if (domainEvents.Count > 0 && domainEventDispatcher is not null)
            {
                await domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);
            }

            return result;
        }

        private List<IDomainEvent> CollectDomainEvents()
        {
            List<IDomainEvent> domainEvents = ChangeTracker
                .Entries<Entity>()
                .SelectMany(entry => entry.Entity.DomainEvents)
                .ToList();

            ChangeTracker
                .Entries<Entity>()
                .ToList()
                .ForEach(entry => entry.Entity.ClearDomainEvents());

            return domainEvents;
        }

        private async Task PersistAuditEntriesAsync(IReadOnlyCollection<AuditEntry> auditEntries, CancellationToken cancellationToken)
        {
            isAuditing = true;

            try
            {
                Set<AuditEntry>().AddRange(auditEntries);
                await base.SaveChangesAsync(true, cancellationToken);
            }
            finally
            {
                isAuditing = false;
            }
        }
    }
}
