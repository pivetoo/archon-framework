namespace Archon.Core.Bulk
{
    /// <summary>
    /// Resultado de uma operacao em lote. Cada registro e processado de forma isolada: a falha de um
    /// nao desfaz nem interrompe os demais, e o motivo fica em <see cref="Failures"/> como chave i18n crua.
    /// </summary>
    public sealed class BulkOperationResult
    {
        private readonly List<long> succeededIds = [];
        private readonly List<BulkOperationFailure> failures = [];

        public BulkOperationResult(int total)
        {
            Total = total;
        }

        public int Total { get; }

        public int Succeeded => succeededIds.Count;

        public int Failed => failures.Count;

        public IReadOnlyList<long> SucceededIds => succeededIds;

        public IReadOnlyList<BulkOperationFailure> Failures => failures;

        public void AddSuccess(long id)
        {
            succeededIds.Add(id);
        }

        public void AddFailure(long id, string messageKey, params object[] messageArgs)
        {
            failures.Add(new BulkOperationFailure(id, messageKey, messageArgs ?? []));
        }
    }

    public sealed record BulkOperationFailure(long Id, string MessageKey, IReadOnlyList<object> MessageArgs);
}
