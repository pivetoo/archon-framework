namespace Archon.Core.Exceptions
{
    /// <summary>
    /// Ação proibida pra esse usuário/contexto. Mapeia para HTTP 403 Forbidden.
    /// Diferente de UnauthorizedAccessException (401): aqui o usuário esta
    /// autenticado mas nao tem permissao pra essa operacao especifica.
    /// </summary>
    public class ForbiddenException : DomainException
    {
        public ForbiddenException(string messageKey, params object[] args) : base(messageKey, args)
        {
        }
    }
}
