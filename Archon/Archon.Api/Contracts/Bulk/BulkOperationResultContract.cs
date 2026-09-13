namespace Archon.Api.Contracts.Bulk
{
    public sealed class BulkOperationResultContract
    {
        public required int Total { get; init; }

        public required int Succeeded { get; init; }

        public required int Failed { get; init; }

        public required IReadOnlyList<long> SucceededIds { get; init; }

        public required IReadOnlyList<BulkOperationFailureContract> Failures { get; init; }
    }

    public sealed class BulkOperationFailureContract
    {
        public required long Id { get; init; }

        /// <summary>Motivo ja traduzido para a cultura da requisicao.</summary>
        public required string Message { get; init; }
    }
}
