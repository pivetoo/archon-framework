namespace Archon.Core.Exceptions
{
    /// <summary>
    /// Recurso nao encontrado. Mapeia para HTTP 404 Not Found.
    /// </summary>
    public class NotFoundException : DomainException
    {
        public NotFoundException(string messageKey, params object[] args) : base(messageKey, args)
        {
        }
    }
}
