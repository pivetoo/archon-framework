namespace Archon.Core.Exceptions
{
    /// <summary>
    /// Conflito de estado. Mapeia para HTTP 409 Conflict.
    /// Use para constraints de unicidade, FK violations, ou conflitos
    /// de versao (ex.: "email ja cadastrado", "registro em uso").
    /// </summary>
    public class ConflictException : DomainException
    {
        public ConflictException(string messageKey, params object[] args) : base(messageKey, args)
        {
        }
    }
}
