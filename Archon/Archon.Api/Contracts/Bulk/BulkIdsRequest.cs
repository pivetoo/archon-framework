namespace Archon.Api.Contracts.Bulk
{
    /// <summary>
    /// Corpo das operacoes em lote: os ids dos registros alvo. A validacao (lista vazia, id invalido e
    /// limite por chamada) fica nos helpers de lote do <c>ApiControllerBase</c>, com mensagem traduzida.
    /// </summary>
    public sealed class BulkIdsRequest
    {
        /// <summary>Limite de registros por chamada, para uma requisicao nao segurar o banco por minutos.</summary>
        public const int MaxItems = 500;

        public List<long> Ids { get; init; } = [];
    }
}
