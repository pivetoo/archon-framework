namespace Archon.Core.Exceptions
{
    /// <summary>
    /// Excecao base do dominio. A Message deve ser uma chave de localizacao
    /// (ex.: "opportunity.pipeline.notConfigured"). O middleware mapeia o tipo
    /// para o status HTTP correto e resolve a chave nos resources da aplicacao.
    /// </summary>
    public abstract class DomainException : Exception
    {
        /// <summary>Argumentos opcionais para interpolar na mensagem localizada.</summary>
        public object[] MessageArgs { get; }

        protected DomainException(string messageKey, params object[] args) : base(messageKey)
        {
            MessageArgs = args ?? [];
        }

        protected DomainException(string messageKey, Exception innerException, params object[] args) : base(messageKey, innerException)
        {
            MessageArgs = args ?? [];
        }
    }
}
