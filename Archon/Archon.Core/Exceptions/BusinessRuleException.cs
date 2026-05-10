namespace Archon.Core.Exceptions
{
    /// <summary>
    /// Regra de negocio violada. Mapeia para HTTP 400 Bad Request.
    /// Use quando uma operacao for valida tecnicamente mas violar uma regra
    /// (ex.: "tentou converter proposta ja convertida", "estagio nao configurado").
    /// </summary>
    public class BusinessRuleException : DomainException
    {
        public BusinessRuleException(string messageKey, params object[] args) : base(messageKey, args)
        {
        }
    }
}
