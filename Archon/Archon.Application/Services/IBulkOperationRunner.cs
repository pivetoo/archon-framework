using Archon.Core.Bulk;
using Archon.Core.Entities;

namespace Archon.Application.Services
{
    /// <summary>
    /// Executa uma operacao sobre varios registros numa unica chamada, isolando cada um: o que falha
    /// entra no resultado com o motivo e o restante segue. Todas as alteracoes da mesma requisicao
    /// compartilham o TraceId, que a auditoria grava como CorrelationId.
    /// </summary>
    public interface IBulkOperationRunner
    {
        /// <summary>
        /// Chama <paramref name="operation"/> uma vez por id, na ordem recebida e sem repeticao. Use para
        /// reaproveitar o metodo de servico que ja aplica as regras do registro (ex.: excluir).
        /// </summary>
        Task<BulkOperationResult> RunAsync(IReadOnlyCollection<long> ids, Func<long, CancellationToken, Task> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ativa ou inativa cada registro carregando a entidade do banco e chamando Activate/Deactivate,
        /// sem depender de dados enviados pelo cliente. Registro que ja esta no estado pedido conta como
        /// sucesso sem gravar nada. <paramref name="validate"/> recebe a entidade antes da mudanca, para
        /// regras que dependem de outros registros (ex.: so pode existir um ativo de certo tipo).
        /// </summary>
        Task<BulkOperationResult> SetActiveAsync<T>(IReadOnlyCollection<long> ids, bool isActive, Func<T, CancellationToken, Task>? validate = null, CancellationToken cancellationToken = default)
            where T : Entity, IActivatable;
    }
}
